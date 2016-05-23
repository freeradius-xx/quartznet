#region License

/* 
 * All content copyright Terracotta, Inc., unless otherwise indicated. All rights reserved. 
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not 
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at 
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0 
 *   
 * Unless required by applicable law or agreed to in writing, software 
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations 
 * under the License.
 * 
 */

#endregion

using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

using Quartz.Impl;
using Quartz.Logging;

namespace Quartz.Examples.Example5
{
    /// <summary> 
    /// Demonstrates the behavior of <see cref="PersistJobDataAfterExecutionAttribute" />, 
    /// as well as how misfire instructions affect the firings of triggers of
    /// that have <see cref="DisallowConcurrentExecutionAttribute" /> present - 
    /// when the jobs take longer to execute that the frequency of the trigger's
    /// repetition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While the example is running, you should note that there are two triggers
    /// with identical schedules, firing identical jobs. The triggers "want" to fire
    /// every 3 seconds, but the jobs take 10 seconds to execute. Therefore, by the
    /// time the jobs complete their execution, the triggers have already "misfired"
    /// (unless the scheduler's "misfire threshold" has been set to more than 7
    /// seconds). You should see that one of the jobs has its misfire instruction
    /// set to <see cref="MisfireInstruction.SimpleTrigger.RescheduleNowWithExistingRepeatCount" />,
    /// which causes it to fire immediately, when the misfire is detected. The other
    /// trigger uses the default "smart policy" misfire instruction, which causes
    /// the trigger to advance to its next fire time (skipping those that it has
    /// missed) - so that it does not refire immediately, but rather at the next
    /// scheduled time.
    /// </para>
    /// </remarks>
    /// <author><a href="mailto:bonhamcm@thirdeyeconsulting.com">Chris Bonham</a></author>
    /// <author>Marko Lahma (.NET)</author>
    public class MisfireExample : IExample
    {
        public string Name
        {
            get { throw new NotImplementedException(); }
        }

        public virtual async Task Run()
        {
            ILog log = LogProvider.GetLogger(typeof (MisfireExample));

            log.Info("------- Initializing -------------------");

            // First we must get a reference to a scheduler
            var properties = new NameValueCollection
            {
                ["quartz.serializer.type"] = "json"
            };
            ISchedulerFactory sf = new StdSchedulerFactory(properties);
            IScheduler sched = await sf.GetScheduler();

            log.Info("------- Initialization Complete -----------");

            log.Info("------- Scheduling Jobs -----------");

            // jobs can be scheduled before start() has been called

            // get a "nice round" time a few seconds in the future...

            DateTimeOffset startTime = DateBuilder.NextGivenSecondDate(null, 15);

            // statefulJob1 will run every three seconds
            // (but it will delay for ten seconds)
            IJobDetail job = JobBuilder.Create<StatefulDumbJob>()
                .WithIdentity("statefulJob1", "group1")
                .UsingJobData(StatefulDumbJob.ExecutionDelay, 10000L)
                .Build();

            ISimpleTrigger trigger = (ISimpleTrigger) TriggerBuilder.Create()
                .WithIdentity("trigger1", "group1")
                .StartAt(startTime)
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(3).RepeatForever())
                .Build();

            DateTimeOffset ft = await sched.ScheduleJob(job, trigger);
            log.Info($"{job.Key} will run at: {ft.ToString("r")} and repeat: {trigger.RepeatCount} times, every {trigger.RepeatInterval.TotalSeconds} seconds");

            // statefulJob2 will run every three seconds
            // (but it will delay for ten seconds - and therefore purposely misfire after a few iterations)
            job = JobBuilder.Create<StatefulDumbJob>()
                .WithIdentity("statefulJob2", "group1")
                .UsingJobData(StatefulDumbJob.ExecutionDelay, 10000L)
                .Build();

            trigger = (ISimpleTrigger) TriggerBuilder.Create()
                .WithIdentity("trigger2", "group1")
                .StartAt(startTime)
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(3)
                    .RepeatForever()
                    .WithMisfireHandlingInstructionNowWithExistingCount()) // set misfire instructions
                .Build();
            ft = await sched.ScheduleJob(job, trigger);

            log.Info($"{job.Key} will run at: {ft.ToString("r")} and repeat: {trigger.RepeatCount} times, every {trigger.RepeatInterval.TotalSeconds} seconds");

            log.Info("------- Starting Scheduler ----------------");

            // jobs don't start firing until start() has been called...
            await sched.Start();

            log.Info("------- Started Scheduler -----------------");

            // sleep for ten minutes for triggers to file....
            await Task.Delay(TimeSpan.FromMinutes(10));

            log.Info("------- Shutting Down ---------------------");

            await sched.Shutdown(true);

            log.Info("------- Shutdown Complete -----------------");

            SchedulerMetaData metaData = await sched.GetMetaData();
            log.Info($"Executed {metaData.NumberOfJobsExecuted} jobs.");
        }
    }
}