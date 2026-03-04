namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Progress information for lookup column analysis operations
/// Supports hierarchical progress reporting with main task and sub-tasks
/// </summary>
public class AnalysisProgress
{
    /// <summary>
    /// Current stage of analysis (e.g., "Loading Source Data", "Semantic Matching")
    /// </summary>
    public string Stage { get; set; } = "";

    /// <summary>
    /// Current step number in the overall process
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Total number of steps in the overall process
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Detailed message about current operation
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Overall percent complete (0-100)
    /// </summary>
    public double PercentComplete => TotalSteps > 0 ? (CurrentStep * 100.0 / TotalSteps) : 0;

    /// <summary>
    /// Sub-task progress (e.g., progress within semantic matching)
    /// </summary>
    public SubProgress? SubTask { get; set; }

    /// <summary>
    /// Overall progress including sub-task (0-100)
    /// </summary>
    public double OverallPercentComplete
    {
        get
        {
            if (TotalSteps == 0) return 0;

            var baseProgress = (CurrentStep - 1) * 100.0 / TotalSteps;
            var stepProgress = 100.0 / TotalSteps;

            if (SubTask != null && SubTask.Total > 0)
            {
                var subTaskPercent = SubTask.Current * 100.0 / SubTask.Total;
                return baseProgress + (stepProgress * subTaskPercent / 100.0);
            }

            return baseProgress + stepProgress;
        }
    }

    /// <summary>
    /// Create a new progress instance for a specific stage
    /// </summary>
    public static AnalysisProgress Create(string stage, int currentStep, int totalSteps, string message = "")
    {
        return new AnalysisProgress
        {
            Stage = stage,
            CurrentStep = currentStep,
            TotalSteps = totalSteps,
            Message = message
        };
    }
}

/// <summary>
/// Sub-task progress information
/// </summary>
public class SubProgress
{
    /// <summary>
    /// Name of the sub-task
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Current item in sub-task
    /// </summary>
    public int Current { get; set; }

    /// <summary>
    /// Total items in sub-task
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Sub-task message
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Sub-task percent complete (0-100)
    /// </summary>
    public double PercentComplete => Total > 0 ? (Current * 100.0 / Total) : 0;

    public static SubProgress Create(string name, int current, int total, string message = "")
    {
        return new SubProgress
        {
            Name = name,
            Current = current,
            Total = total,
            Message = message
        };
    }
}
