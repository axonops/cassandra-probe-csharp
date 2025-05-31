using CassandraProbe.Core.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CassandraProbe.Scheduling;

public class JobScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<JobScheduler> _logger;
    private IScheduler? _scheduler;

    public JobScheduler(ISchedulerFactory schedulerFactory, ILogger<JobScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task<IScheduler> StartAsync(ProbeConfiguration configuration)
    {
        _scheduler = await _schedulerFactory.GetScheduler();
        await _scheduler.Start();

        _logger.LogInformation("Scheduler started");

        if (configuration.Scheduling.IntervalSeconds.HasValue || 
            !string.IsNullOrEmpty(configuration.Scheduling.CronExpression))
        {
            await ScheduleProbeJob(configuration);
        }

        return _scheduler;
    }

    public async Task StopAsync()
    {
        if (_scheduler != null && !_scheduler.IsShutdown)
        {
            _logger.LogInformation("Shutting down scheduler...");
            await _scheduler.Shutdown(waitForJobsToComplete: true);
            _logger.LogInformation("Scheduler shutdown complete");
        }
    }

    private async Task ScheduleProbeJob(ProbeConfiguration configuration)
    {
        var jobKey = new JobKey("ProbeJob", "CassandraProbe");
        
        // Create job
        var job = JobBuilder.Create<ProbeJob>()
            .WithIdentity(jobKey)
            .UsingJobData(new JobDataMap { { "ProbeConfiguration", configuration } })
            .Build();

        // Create trigger
        ITrigger trigger;
        
        if (!string.IsNullOrEmpty(configuration.Scheduling.CronExpression))
        {
            _logger.LogInformation("Scheduling probe job with cron expression: {Cron}", 
                configuration.Scheduling.CronExpression);
            
            trigger = TriggerBuilder.Create()
                .WithIdentity("ProbeTrigger", "CassandraProbe")
                .WithCronSchedule(configuration.Scheduling.CronExpression)
                .Build();
        }
        else if (configuration.Scheduling.IntervalSeconds.HasValue)
        {
            _logger.LogInformation("Scheduling probe job with interval: {Interval} seconds", 
                configuration.Scheduling.IntervalSeconds.Value);

            var triggerBuilder = TriggerBuilder.Create()
                .WithIdentity("ProbeTrigger", "CassandraProbe")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(configuration.Scheduling.IntervalSeconds.Value)
                    .RepeatForever());

            // Apply max runs if specified
            if (configuration.Scheduling.MaxRuns.HasValue)
            {
                triggerBuilder.WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(configuration.Scheduling.IntervalSeconds.Value)
                    .WithRepeatCount(configuration.Scheduling.MaxRuns.Value - 1));
            }

            // Apply duration limit if specified
            if (configuration.Scheduling.DurationMinutes.HasValue)
            {
                var endTime = DateTimeOffset.UtcNow.AddMinutes(configuration.Scheduling.DurationMinutes.Value);
                triggerBuilder.EndAt(endTime);
            }

            trigger = triggerBuilder.Build();
        }
        else
        {
            _logger.LogWarning("No scheduling configuration found");
            return;
        }

        await _scheduler!.ScheduleJob(job, trigger);
        
        _logger.LogInformation("Probe job scheduled successfully. First run: {FirstRun}", 
            trigger.GetNextFireTimeUtc()?.DateTime ?? DateTime.MaxValue);
    }
}