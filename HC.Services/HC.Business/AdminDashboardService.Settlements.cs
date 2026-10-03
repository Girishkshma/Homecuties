using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// The gateway's own books, as the admin area asks for them: one action that reads what Razorpay settled to the
/// shop's bank account and writes down what it settled it on (see <see cref="RazorpaySettlements.SyncAsync"/>,
/// where the whole of it lives - this is only the door the admin app knocks on).
///
/// It is the same pull the server runs by itself (see HC.Services/SettlementSyncJob), deliberately: a shop team
/// member looking at a day the books do not explain can pull it again from the screen and read the answer in the
/// same words the log uses, rather than having to wait for the next pass and guess.
/// </summary>
public partial class AdminDashboardService
{
    /// <summary>
    /// Pulls the gateway's books over the asked-for window (the rolling one - yesterday and the seven days before
    /// it - when none is given) and answers with what the pull did: how many days were read, how many lines were
    /// written down, how many payment rows the settlement's own charge corrected, and every finding the pull made
    /// (a day Razorpay would not answer for, a settled payment the shop has no row for, a line whose amount or
    /// whose arithmetic does not agree with what is recorded here).
    ///
    /// Result = 1 when every day asked for was answered, 0 when any of them could not be - a partly read window is
    /// reported as such rather than as a failure of the whole, and the messages say which day it was.
    ///
    /// The sentence the shop team reads first is the pull's own ('SettlementSyncOutcome.Summary'), not one written
    /// here: the scheduler that runs the same pull logs that very sentence, so what the screen says and what the
    /// server logged can never drift apart.
    /// </summary>
    public async Task<AdminResultDto> SyncSettlementsAsync(DateTime? from, DateTime? to)
    {
        var outcome = await RazorpaySettlements.SyncAsync(
            _context,
            _razorpayKeyId,
            _razorpayKeySecret,
            from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
            to.HasValue ? DateOnly.FromDateTime(to.Value) : null);

        var messages = new List<string> { outcome.Summary };
        messages.AddRange(outcome.Messages);

        return new AdminResultDto
        {
            Result = outcome.Succeeded ? 1 : 0,
            Messages = messages.ToArray()
        };
    }
}
