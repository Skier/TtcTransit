namespace TtcTransit.Web.Models;

public sealed class StopDetailsViewModel
{
    public string StopId { get; init; } = "";
    public DateOnly Date { get; init; }

    public IReadOnlyList<StopScheduleItemViewModel> Schedule { get; init; }
        = Array.Empty<StopScheduleItemViewModel>();

    public IReadOnlyList<RealtimeStopArrivalViewModel> Realtime { get; init; }
        = Array.Empty<RealtimeStopArrivalViewModel>();
}