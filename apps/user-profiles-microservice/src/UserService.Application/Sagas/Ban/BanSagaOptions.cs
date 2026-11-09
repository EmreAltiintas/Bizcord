namespace UserService.Application.Sagas.Ban;

public sealed class BanSagaOptions
{
    public const string SectionName = "BanSaga";

    /// <summary>Attempts per step, including the first one.</summary>
    public int MaxStepAttempts { get; set; } = 3;

    /// <summary>Delay before the first retry. It doubles for each further retry.</summary>
    public TimeSpan BackoffBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}
