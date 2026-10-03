using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// What a settlement recon pull did (<see cref="RazorpaySettlements.SyncAsync"/>), counted so the admin
/// screen and the log can say it in one sentence: how many days were read, how many lines were written into
/// the ledger for the first time, how many payment rows the authoritative charge corrected, and - the numbers
/// that matter most - how many settled payments the shop has no row for, how many settlement lines carried a
/// different amount than the payment row did, and how many lines Razorpay's own credit disagreed with
/// amount - fee - GST on.
/// </summary>
public sealed record SettlementSyncOutcome(
    int DaysPulled,
    int LinesStored,
    int PaymentsCorrected,
    int PaymentsNotFound,
    int AmountMismatches,
    int CreditMismatches,
    int RefundsRefreshed,
    int FailedDays,
    IReadOnlyList<string> Messages)
{
    /// <summary>True when every day asked for was answered (a day the gateway refused is reported, not swallowed).</summary>
    public bool Succeeded => FailedDays == 0;

    /// <summary>
    /// What the pull did, in the one sentence the shop team reads first. The admin screen's answer to 'Pull
    /// now' and the scheduler's log line are both this (<see cref="RazorpaySettlements.SyncAsync"/> is the one
    /// pull behind both), so the same pull can never be described two ways. The detail follows as
    /// <see cref="Messages"/>, and a pull that changed nothing says so - a green answer must never be mistaken
    /// for having found nothing wrong.
    /// </summary>
    public string Summary
    {
        get
        {
            var summary = $"{DaysPulled} day(s) read, {LinesStored} settlement line(s) written down, " +
                $"{PaymentsCorrected} payment(s) charged the settled figure, " +
                $"{RefundsRefreshed} refund(s) brought up to date";

            if (PaymentsNotFound == 0 && AmountMismatches == 0 && CreditMismatches == 0)
                return summary + " - nothing here needed a second look.";

            // The wording 'I' is deliberate: it is the pull speaking, and the shop team reads these three
            // figures as the questions the books cannot answer on their own.
            return summary + $": {PaymentsNotFound} payment(s) with no row here, " +
                $"{AmountMismatches} line(s) disagreeing with a payment row, " +
                $"{CreditMismatches} line(s) whose credit I cannot account for.";
        }
    }
}

/// <summary>
/// The gateway's own books: what Razorpay settled to the shop's bank account and what it settled it on - the
/// reconciliation pull behind the two settlement tables (see CreateSettlementTables.sql) and the Finance
/// screen's income statement.
///
/// Two questions are answered here that nothing else in the system could answer:
///
///   1. WHAT DID THE GATEWAY KEEP? The payment entity's own <c>fee</c>/<c>tax</c>, written by a capture (see
///      <see cref="OrderPaymentCharges.ApplyCapture"/>), is an estimate read off whichever payload happened to
///      be at hand. The settlement recon report is the figure the bank was actually settled on, so the pull
///      writes it over the estimate and marks the row <see cref="OrderPaymentCharges.FromRecon"/> - which
///      nothing may overwrite back, a replayed webhook least of all. Until this ran, a paid order's screen
///      could not say whether it made money.
///
///   2. WHAT DID THE SHOP ACTUALLY RECEIVE, AND WHEN? A payment and its settlement are different days, a
///      settlement covers however many payments were in it, a refund nets off against it and a chargeback or
///      an adjustment lands on it. <see cref="Settlement"/> and <see cref="SettlementItem"/> are that ledger,
///      written whole and compared against the payment rows rather than merged into them (see
///      <see cref="AmountDisagrees"/>).
///
/// A pull is idempotent by construction: a line is identified by Razorpay's own id of the thing it is plus
/// its type (a unique index, see the script), so pulling a day twice updates those lines and adds none. That
/// is what makes the rolling re-pull of the last few days safe - a settlement can still be created, be on
/// hold, or have its charges corrected a day after the transaction it covers.
///
/// What this deliberately does NOT do:
///   * it does not move money or change an order, a refund or a parcel - a refund that came back on a
///     settlement line is still the refund of the payment row (<see cref="RazorpayRefunds"/>);
///   * it does not invent a payment row for a settled payment the shop has never seen. That is a real
///     reconciliation finding (a payment taken in the Razorpay dashboard, say), and it is counted and
///     reported as <see cref="SettlementSyncOutcome.PaymentsNotFound"/> rather than silently created - which
///     payment it was and which order it belongs to is a question only a human can answer.
///
/// The one thing here that cannot be reasoned about is whether Razorpay's <c>fee</c> already includes the GST
/// on it: the recon report carries both <c>fee</c> and <c>tax</c>, sometimes with a tax of 0 (which would mean
/// the fee is GST-inclusive, or that there is no GST to add). Nothing here guesses - a line stores the
/// gateway's own <c>credit</c> AND the figure amount - fee - GST works out to (see
/// <see cref="SettlementItem.NetAmount"/>), and <see cref="SettlementReconLine.CreditAgreesWithFee"/> is what
/// says which of the two the shop's own settlement data supports. Where they disagree the pull says so in its
/// messages, so the answer comes from live settlements instead of from an assumption baked into the
/// arithmetic.
/// </summary>
public static class RazorpaySettlements
{
    private static readonly HttpClient Http = new();

    /// <summary>Razorpay's REST endpoint root (the same one <see cref="RazorpayRefunds"/> talks to).</summary>
    private const string ApiRoot = "https://api.razorpay.com/v1";

    // ---------------------------------------------------------------------------------------------
    // Razorpay's own words. The SettlementItems.ItemType column holds these and the CHECK constraint in
    // CreateSettlementTables.sql holds the same set, so a kind of line this code does not understand is
    // skipped by the reader rather than stored under a word nothing can read back.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A payment that was settled to the bank (the line's entity id is the <c>pay_xxx</c>).</summary>
    public const string ItemTypePayment = "payment";

    /// <summary>A refund taken out of the settlement (the line's entity id is the <c>rfnd_xxx</c>).</summary>
    public const string ItemTypeRefund = "refund";

    /// <summary>An adjustment Razorpay applied to the settlement (a chargeback, a correction).</summary>
    public const string ItemTypeAdjustment = "adjustment";

    /// <summary>A transfer line of the settlement report.</summary>
    public const string ItemTypeTransfer = "transfer";

    /// <summary>Every kind of settlement line this code reads (the CHECK constraint holds the same set).</summary>
    public static readonly string[] ItemTypes =
        { ItemTypePayment, ItemTypeRefund, ItemTypeAdjustment, ItemTypeTransfer };

    /// <summary>Razorpay has created the settlement but not sent the money yet.</summary>
    public const string StatusCreated = "created";

    /// <summary>The money is in the shop's bank account.</summary>
    public const string StatusProcessed = "processed";

    /// <summary>Razorpay could not settle it - the money is still with the gateway and has to be chased.</summary>
    public const string StatusFailed = "failed";

    /// <summary>
    /// The window a plain sync pulls: yesterday and the seven days before it. Long enough for a settlement
    /// that was created late, was on hold and then went through, or whose charges were corrected after the
    /// transaction it covers - Razorpay reports all of those against the day of the transaction, not the day
    /// of the change.
    /// </summary>
    public const int LookbackDays = 8;

    /// <summary>
    /// How many days one pull may cover. Every day is a request to the gateway, so a window asked for too
    /// wide is trimmed rather than turned into hundreds of calls - catching up over a longer gap is a
    /// deliberate act, day by day (see <see cref="ResolvePullDays"/>).
    /// </summary>
    public const int MaxPullDays = 31;

    /// <summary>True when both Razorpay credentials are present (the gateway can be asked).</summary>
    public static bool IsConfigured(string keyId, string keySecret) =>
        !string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(keySecret);

    /// <summary>
    /// One line of the settlement recon report (<c>GET /v1/settlements/recon/combined</c>), read into the
    /// shape this system thinks in rather than carried around as a JSON element. Everything Razorpay said is
    /// kept, and the figures the books need are worked out BESIDE it - never over it, so a disagreement is
    /// visible instead of lost.
    /// </summary>
    public sealed record SettlementReconLine(
        string EntityId,
        string ItemType,
        string? PaymentId,
        string? OrderId,
        string? SettlementId,
        string? Utr,
        int AmountInPaise,
        int DebitInPaise,
        int CreditInPaise,
        int FeeAmountInPaise,
        int TaxAmountInPaise,
        string? Method,
        bool OnHold,
        bool Settled,
        string? Description,
        string? DisputeId,
        DateTime? CreatedOn,
        DateTime? SettledOn)
    {
        /// <summary>True for the line of a settled payment - the only kind that corrects a payment row's charges.</summary>
        public bool IsPayment => ItemType == ItemTypePayment;

        /// <summary>True for a refund that was taken out of the settlement.</summary>
        public bool IsRefund => ItemType == ItemTypeRefund;

        /// <summary>
        /// The refund this line is, on a refund line: Razorpay numbers a refund line by the refund itself, so
        /// this is the entity id restated where the code looks for it by name.
        /// </summary>
        public string? RefundId => IsRefund ? EntityId : null;

        /// <summary>
        /// The payment behind the line: the line's own entity id on a payment line (Razorpay leaves
        /// <c>payment_id</c> null there), and Razorpay's <c>payment_id</c> on a refund or adjustment line.
        /// </summary>
        public string? PaymentBehind => IsPayment ? EntityId : PaymentId;

        /// <summary>What the settlement did for the shop on this line, in rupees: paid in (credit) less taken out (debit).</summary>
        public decimal Effect => (CreditInPaise - DebitInPaise) / 100m;

        /// <summary>What the settlement paid in for this line (a payment), in rupees.</summary>
        public decimal Credit => CreditInPaise / 100m;

        /// <summary>What the settlement took out for this line (a refund, an adjustment), in rupees.</summary>
        public decimal Debit => DebitInPaise / 100m;

        /// <summary>The size of the transaction behind the line, in rupees.</summary>
        public decimal Amount => AmountInPaise / 100m;

        /// <summary>What the gateway kept for taking the money, in rupees.</summary>
        public decimal FeeAmount => FeeAmountInPaise / 100m;

        /// <summary>GST on the gateway's charge, in rupees.</summary>
        public decimal TaxAmount => TaxAmountInPaise / 100m;

        /// <summary>
        /// What the shop keeps out of this line, worked out the one way this system offers
        /// (<see cref="OrderPaymentCharges.Net"/>): the amount minus the gateway's charge minus the GST on it
        /// for a payment - and what the settlement moved for anything else, where a refund pays out and so
        /// comes out negative.
        /// </summary>
        public decimal NetAmount =>
            IsPayment ? OrderPaymentCharges.Net(Amount, FeeAmount, TaxAmount) : Effect;

        /// <summary>
        /// True when Razorpay's own <c>credit</c> is exactly what amount - fee - GST works out to, i.e. when
        /// its <c>fee</c> is GST-EXCLUSIVE (the arithmetic this system's net columns assume). False means the
        /// credit says something else - most tellingly <c>credit = amount - fee</c> with a non-zero
        /// <c>tax</c>, which is what a GST-INCLUSIVE fee looks like. Only a payment line can answer it: every
        /// other kind of line moves a figure rather than a sale.
        /// </summary>
        public bool CreditAgreesWithFee =>
            !IsPayment ||
            OrderPaymentCharges.Net(AmountInPaise, FeeAmountInPaise, TaxAmountInPaise) == CreditInPaise;
    }

    /// <summary>
    /// The window a pull should cover, from what the caller asked for: nothing asked for means the plain
    /// rolling window (yesterday and the seven days before it, <see cref="LookbackDays"/>), the far end is
    /// never later than today (a settlement is made after the money moved, so the gateway has nothing for
    /// tomorrow), a window wider than <see cref="MaxPullDays"/> is trimmed at its far end (the recent days are
    /// what a catch-up needs first), and a window that runs backwards is read as the single day it names.
    /// Pure, so the rule can be read and pinned without a gateway.
    /// </summary>
    public static (DateOnly From, DateOnly To) ResolvePullDays(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var last = to ?? today.AddDays(-1);

        if (last > today)
            last = today;

        var first = from ?? last.AddDays(-(LookbackDays - 1));

        // Trim at the far end rather than the near one: a long catch-up is run from the newest day backwards
        // and the rest asked for in another pull, so the days most likely to still change are read first.
        var oldest = last.AddDays(-(MaxPullDays - 1));
        if (first < oldest)
            first = oldest;

        if (first > last)
            first = last;

        return (first, last);
    }

    /// <summary>
    /// Every line of a reconciliation report this code can read (the <c>items</c> collection of
    /// <c>GET /v1/settlements/recon/combined</c>). A payload that is not a collection, and an item with no id
    /// of its own or a kind this code does not read, is skipped rather than stored under a word nothing can
    /// look up again: the report is an external contract, and a shape change in it must not put rows in the
    /// ledger that no screen can make sense of.
    /// </summary>
    public static List<SettlementReconLine> ParseRecon(JsonElement payload)
    {
        var lines = new List<SettlementReconLine>();

        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return lines;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (TryParseReconLine(item, out var line))
                lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// One line of a recon report, or false when the item is not one this code can read (see
    /// <see cref="ParseRecon"/>). Every field is optional as far as the books are concerned: a field Razorpay
    /// stops sending reads as absent, which the ledger records as it was written rather than as a zero that
    /// would look like a real figure.
    /// </summary>
    public static bool TryParseReconLine(JsonElement item, out SettlementReconLine line)
    {
        line = null!;

        if (item.ValueKind != JsonValueKind.Object)
            return false;

        var entityId = ReadString(item, "entity_id");
        var itemType = ReadString(item, "type");

        if (string.IsNullOrWhiteSpace(entityId) ||
            string.IsNullOrWhiteSpace(itemType) ||
            Array.IndexOf(ItemTypes, itemType) < 0)
        {
            return false;
        }

        line = new SettlementReconLine(
            entityId,
            itemType,
            ReadString(item, "payment_id"),
            ReadString(item, "order_id"),
            ReadString(item, "settlement_id"),
            ReadString(item, "settlement_utr"),
            ReadInt(item, "amount") ?? 0,
            ReadInt(item, "debit") ?? 0,
            ReadInt(item, "credit") ?? 0,
            ReadInt(item, "fee") ?? 0,
            ReadInt(item, "tax") ?? 0,
            ReadString(item, "method"),
            ReadBool(item, "on_hold"),
            ReadBool(item, "settled"),
            ReadString(item, "description"),
            ReadString(item, "dispute_id"),
            ReadTimestamp(item, "created_at"),
            ReadTimestamp(item, "settled_at"));

        return true;
    }

    /// <summary>
    /// A recon line written down as a new ledger row (SettlementItems). <see cref="ApplyReconLine"/> does the
    /// work - creating is just an update of a row that remembers nothing yet - so a row read for the first
    /// time and the same row read again can never be filled in differently.
    /// </summary>
    public static SettlementItem FromReconLine(
        SettlementReconLine line,
        long? settlementId,
        DateTime reconDay,
        DateTime now)
    {
        var item = new SettlementItem
        {
            RazorpayEntityId = line.EntityId,
            ItemType = line.ItemType,
            CreatedOn = now
        };

        ApplyReconLine(item, line, settlementId, reconDay);

        return item;
    }

    /// <summary>
    /// Writes a recon line onto a ledger row, whole and unchanged: what Razorpay said, in paise and in rupees,
    /// with the ids that trace it back to an order's payment, plus what the books work out themselves (what
    /// the settlement moved, <see cref="SettlementItem.NetAmount"/>) and which day's report it was read from.
    /// Returns true when anything changed - which is how a re-pull of a day it has already stored adds
    /// nothing, and how the day a settlement went through (or was put on hold) is picked up.
    ///
    /// Razorpay's own spelling of the line is written as given, so a figure it corrects later is corrected
    /// here too - this table is the gateway's report of the money, not an opinion about it. What a report does
    /// NOT carry this time keeps whatever the row already knows (<see cref="Keep(string?, string?)"/>): a line
    /// that settled money is not blanked out by the next pull's silence.
    /// </summary>
    public static bool ApplyReconLine(
        SettlementItem item,
        SettlementReconLine line,
        long? settlementId,
        DateTime reconDay)
    {
        var changed = false;

        // The settlement the line came in with. A line whose report named one the pull has not fetched yet
        // keeps whatever the row already knew rather than being blanked back out.
        changed |= Changed<long?>(
            item.SettlementId, settlementId ?? item.SettlementId, v => item.SettlementId = v);

        changed |= Changed<string?>(
            item.RazorpayPaymentId, Keep(item.RazorpayPaymentId, line.PaymentBehind),
            v => item.RazorpayPaymentId = v);
        changed |= Changed<string?>(
            item.RazorpayOrderId, Keep(item.RazorpayOrderId, line.OrderId),
            v => item.RazorpayOrderId = v);
        changed |= Changed<string?>(
            item.RazorpayRefundId, Keep(item.RazorpayRefundId, line.RefundId),
            v => item.RazorpayRefundId = v);
        changed |= Changed<string?>(
            item.RazorpaySettlementId, Keep(item.RazorpaySettlementId, line.SettlementId),
            v => item.RazorpaySettlementId = v);
        changed |= Changed<string?>(
            item.SettlementUtr, Keep(item.SettlementUtr, line.Utr), v => item.SettlementUtr = v);

        // The money, as Razorpay speaks it (paise) and in rupees beside it.
        changed |= Changed<int?>(item.AmountInPaise, line.AmountInPaise, v => item.AmountInPaise = v);
        changed |= Changed<decimal?>(item.Amount, line.Amount, v => item.Amount = v);
        changed |= Changed<int?>(item.DebitInPaise, line.DebitInPaise, v => item.DebitInPaise = v);
        changed |= Changed<decimal?>(item.Debit, line.DebitInPaise / 100m, v => item.Debit = v);
        changed |= Changed<int?>(item.CreditInPaise, line.CreditInPaise, v => item.CreditInPaise = v);
        changed |= Changed<decimal?>(item.Credit, line.CreditInPaise / 100m, v => item.Credit = v);

        changed |= Changed<int?>(item.FeeAmountInPaise, line.FeeAmountInPaise, v => item.FeeAmountInPaise = v);
        changed |= Changed<decimal?>(item.FeeAmount, line.FeeAmount, v => item.FeeAmount = v);
        changed |= Changed<int?>(item.TaxAmountInPaise, line.TaxAmountInPaise, v => item.TaxAmountInPaise = v);
        changed |= Changed<decimal?>(item.TaxAmount, line.TaxAmount, v => item.TaxAmount = v);

        // Amount - fee - GST, stored beside the gateway's own credit so the two can be compared later.
        changed |= Changed<decimal?>(item.NetAmount, line.NetAmount, v => item.NetAmount = v);

        changed |= Changed<string?>(
            item.PaymentMethod, Keep(item.PaymentMethod, Truncate(line.Method, 30)),
            v => item.PaymentMethod = v);
        changed |= Changed<bool?>(item.IsOnHold, line.OnHold, v => item.IsOnHold = v);
        changed |= Changed<bool?>(item.IsSettled, line.Settled, v => item.IsSettled = v);

        changed |= Changed<DateTime?>(
            item.GatewayCreatedOn, Keep(item.GatewayCreatedOn, line.CreatedOn),
            v => item.GatewayCreatedOn = v);
        changed |= Changed<DateTime?>(
            item.SettledOn, Keep(item.SettledOn, line.SettledOn), v => item.SettledOn = v);

        changed |= Changed<string?>(
            item.DisputeId, Keep(item.DisputeId, line.DisputeId), v => item.DisputeId = v);
        changed |= Changed<string?>(
            item.Description, Keep(item.Description, Truncate(line.Description, 200)),
            v => item.Description = v);

        // Which day's report FIRST saw it, and never anything else: a line is only ever reported against the day
        // of the transaction it covers, so a later pull reading the same line again is reading the same day's
        // report (and a row stored before this column existed gets it filled in).
        changed |= Changed<DateTime?>(item.ReconDay, item.ReconDay ?? reconDay.Date, v => item.ReconDay = v);

        return changed;
    }

    /// <summary>
    /// What a value read off a report becomes when the report does not carry it this time: what the row already
    /// knows. A re-pull may not blank a line - a field Razorpay leaves out is not the gateway saying the value is
    /// gone, and this ledger is the record of what settled rather than of the latest answer.
    /// </summary>
    private static string? Keep(string? current, string? reported) => reported ?? current;

    /// <summary>See <see cref="Keep(string?, string?)"/> - the same rule for a value type.</summary>
    private static T? Keep<T>(T? current, T? reported) where T : struct => reported ?? current;

    /// <summary>
    /// Sets <paramref name="assign"/> when the value really is different, and says whether it did. The
    /// "did anything change" bookkeeping every writer here shares, so a pull can report what it changed
    /// instead of writing the same rows back on every run.
    /// </summary>
    public static bool Changed<T>(T current, T next, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, next))
            return false;

        assign(next);

        return true;
    }

    /// <summary>
    /// Writes what the settlement list said onto the header row the pull already has (the two are matched by
    /// Razorpay's own settlement id), and says whether anything changed - a settlement that was created and
    /// then processed, or one that was put on hold and then went through, is a change in the header and
    /// nothing else.
    ///
    /// The day its lines were settled is deliberately NOT taken from here: only the lines know that (a
    /// settlement entity of the list carries no settled date), so the pull sets it as it reads them - and the
    /// row keeps the earliest day it has seen, because a re-pull must never make the books jump forward.
    /// </summary>
    public static bool CopySettlement(Settlement target, Settlement source)
    {
        var changed = false;

        changed |= Changed<string?>(target.Utr, source.Utr, v => target.Utr = v);
        changed |= Changed<int?>(target.AmountInPaise, source.AmountInPaise, v => target.AmountInPaise = v);
        changed |= Changed<decimal?>(target.Amount, source.Amount, v => target.Amount = v);
        changed |= Changed<int?>(target.FeesInPaise, source.FeesInPaise, v => target.FeesInPaise = v);
        changed |= Changed<decimal?>(target.Fees, source.Fees, v => target.Fees = v);
        changed |= Changed<int?>(target.TaxInPaise, source.TaxInPaise, v => target.TaxInPaise = v);
        changed |= Changed<decimal?>(target.Tax, source.Tax, v => target.Tax = v);
        changed |= Changed<string?>(target.Currency, source.Currency, v => target.Currency = v);
        changed |= Changed<string?>(target.Status, source.Status, v => target.Status = v);
        changed |= Changed<DateTime?>(
            target.GatewayCreatedOn, source.GatewayCreatedOn, v => target.GatewayCreatedOn = v);

        return changed;
    }

    /// <summary>
    /// The settlements of a settlement-list answer, as rows to be written down. Only what that list carries is
    /// read: the fee and tax totals here are Razorpay's own summary of a settlement (0 for the ordinary one,
    /// whose fees were taken per payment) - the charge on each payment comes from the recon report, which is
    /// this same pull's other half.
    /// </summary>
    private static List<Settlement> ParseSettlements(JsonElement payload, DateTime now)
    {
        var settlements = new List<Settlement>();

        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return settlements;
        }

        foreach (var item in items.EnumerateArray())
        {
            var id = ReadString(item, "id");

            // A settlement without an id of its own cannot be matched to anything later, so it is skipped
            // rather than stored as a row nothing can read back.
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var amount = ReadInt(item, "amount");
            var fees = ReadInt(item, "fees");
            var tax = ReadInt(item, "tax");

            settlements.Add(new Settlement
            {
                RazorpaySettlementId = Truncate(id, 50)!,
                Utr = Truncate(ReadString(item, "utr"), 50),
                AmountInPaise = amount,
                Amount = amount / 100m,
                FeesInPaise = fees,
                Fees = fees / 100m,
                TaxInPaise = tax,
                Tax = tax / 100m,
                Currency = Truncate(ReadString(item, "currency"), 3),
                Status = Truncate(ReadString(item, "status"), 20),
                GatewayCreatedOn = ReadTimestamp(item, "created_at"),
                CreatedOn = now
            });
        }

        return settlements;
    }

    /// <summary>
    /// Corrects a payment row's gateway charges with the authoritative figure off the settlement line - the
    /// one the bank was actually settled on. Returns true when the row changed. Called by the pull
    /// (<see cref="SyncAsync"/>) for every settled payment it recognises.
    ///
    /// Three rules, and each of them exists because the alternative is money the books cannot explain:
    ///
    ///   * the charges are only taken when the line really carries one (or when the row still knows nothing).
    ///     A recon line of a payment whose fee Razorpay has not worked out yet reports 0/0, and that must not
    ///     blank out a figure the capture already reported;
    ///   * once taken, the recon's word is final - the row is marked <see cref="OrderPaymentCharges.FromRecon"/>
    ///     and nothing may correct it back, a webhook replay least of all
    ///     (<see cref="OrderPaymentCharges.IsAuthoritative"/>);
    ///   * what the shop keeps is worked out the one way this system offers - payment amount minus fee minus
    ///     GST - and is (re)written even when the charges were already the recon's, because the payment's own
    ///     amount may have been corrected since.
    ///
    /// The instrument and the moment the money was taken are filled in from the same line when the payment row
    /// does not know them yet, so an order paid before these columns existed is completed by the pull rather
    /// than staying blank.
    /// </summary>
    public static bool ApplyReconCharges(OrderPayment payment, SettlementReconLine line)
    {
        if (!line.IsPayment)
            return false;

        var changed = false;

        var lineCarriesACharge = line.FeeAmountInPaise != 0 || line.TaxAmountInPaise != 0;
        var rowKnowsNothing = (payment.FeeAmountInPaise ?? 0) == 0 && (payment.TaxAmountInPaise ?? 0) == 0;

        if (!OrderPaymentCharges.IsAuthoritative(payment.ChargesSource) &&
            (lineCarriesACharge || rowKnowsNothing))
        {
            if (payment.FeeAmountInPaise != line.FeeAmountInPaise || payment.FeeAmount != line.FeeAmount)
            {
                payment.FeeAmountInPaise = line.FeeAmountInPaise;
                payment.FeeAmount = line.FeeAmount;
            }

            if (payment.TaxAmountInPaise != line.TaxAmountInPaise || payment.TaxAmount != line.TaxAmount)
            {
                payment.TaxAmountInPaise = line.TaxAmountInPaise;
                payment.TaxAmount = line.TaxAmount;
            }

            // The settlement is what the bank was paid on: from here on this row's charges are the gateway's
            // own authoritative word.
            payment.ChargesSource = OrderPaymentCharges.FromRecon;
            changed = true;
        }

        if (string.IsNullOrEmpty(payment.PaymentMethod) && !string.IsNullOrWhiteSpace(line.Method))
        {
            payment.PaymentMethod = Truncate(line.Method, 30);
            changed = true;
        }

        if (payment.GatewayChargedOn == null && line.CreatedOn != null)
        {
            payment.GatewayChargedOn = line.CreatedOn;
            changed = true;
        }

        if (payment.Amount > 0)
        {
            var net = OrderPaymentCharges.Net(payment.Amount, payment.FeeAmount ?? 0m, payment.TaxAmount ?? 0m);

            if (payment.NetAmount != net)
            {
                payment.NetAmount = net;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// True when a settlement line and the payment row it was matched to do not describe the same money: the
    /// payment row is the size the shop took, the line is what the settlement moved for it. Both are already
    /// whatever the books accepted (the recon figure wins where the two met, see
    /// <see cref="ApplyReconCharges"/>), so a difference here is a real reconciliation finding for a human - a
    /// partial settlement, a line landed against the wrong payment, or an amount Razorpay corrected after the
    /// fact - and is reported rather than corrected silently.
    /// </summary>
    public static bool AmountDisagrees(OrderPayment payment, SettlementReconLine line) =>
        line.IsPayment && line.AmountInPaise != 0 && payment.AmountInPaise != line.AmountInPaise;

    /// <summary>
    /// The reconciliation report Razorpay keeps for one day: <c>GET /v1/settlements/recon/combined</c>, which
    /// is the day's settlements broken down into the payments, refunds and adjustments they were made of.
    /// Returns the lines read, an empty list for a day the gateway answered with nothing (a genuinely quiet
    /// day), or null when the gateway could not be asked or refused - which the caller reports rather than
    /// leaving a day that could not be read looking like a day with no settlements.
    ///
    /// The report is read in pages: Razorpay caps what one answer carries, so a day is asked for again from
    /// where the last answer stopped until one comes back shorter than the page (or repeats itself, which a
    /// gateway that ignores the offset would do - stopped rather than looped).
    /// </summary>
    public static async Task<List<SettlementReconLine>?> FetchReconAsync(
        string keyId,
        string keySecret,
        DateOnly day,
        int maxPages = 5)
    {
        var lines = new List<SettlementReconLine>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 0; page < maxPages; page++)
        {
            var payload = await GetAsync(keyId, keySecret,
                $"/settlements/recon/combined?from={EpochSeconds(day, false)}&to={EpochSeconds(day, true)}" +
                $"&count={PageSize}&skip={lines.Count}");

            // A page that failed after a good one keeps what was already read: half a day is worth more than
            // no day, and the next pull asks for the day again anyway.
            if (payload == null)
                return page == 0 ? null : lines;

            var pageLines = ParseRecon(payload.Value);

            if (pageLines.Count == 0)
                return lines;

            var added = 0;

            foreach (var line in pageLines)
            {
                // Razorpay's own id of the thing plus its kind is what identifies a line (the unique index in
                // CreateSettlementTables.sql says the same), so a page that overlaps another cannot store one
                // line twice.
                if (seen.Add($"{line.EntityId}|{line.ItemType}"))
                {
                    lines.Add(line);
                    added++;
                }
            }

            if (added < pageLines.Count || pageLines.Count < PageSize)
                return lines;
        }

        return lines;
    }

    /// <summary>
    /// The settlements Razorpay made to the shop's bank account over a window: <c>GET /v1/settlements/</c>,
    /// read into <see cref="Settlement"/> rows that are not yet tracked (the pull decides which of them it
    /// already has - see <see cref="CopySettlement"/>). Empty when the window holds none, and the same
    /// null-on-refusal rule as <see cref="FetchReconAsync"/>.
    /// </summary>
    public static async Task<List<Settlement>?> FetchSettlementsAsync(
        string keyId,
        string keySecret,
        DateOnly from,
        DateOnly to,
        DateTime now,
        int maxPages = 5)
    {
        var settlements = new List<Settlement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var page = 0; page < maxPages; page++)
        {
            var payload = await GetAsync(keyId, keySecret,
                $"/settlements/?from={EpochSeconds(from, false)}&to={EpochSeconds(to, true)}" +
                $"&count={PageSize}&skip={settlements.Count}");

            if (payload == null)
                return page == 0 ? null : settlements;

            var pageSettlements = ParseSettlements(payload.Value, now);

            if (pageSettlements.Count == 0)
                return settlements;

            var added = 0;

            foreach (var settlement in pageSettlements)
            {
                if (seen.Add(settlement.RazorpaySettlementId))
                {
                    settlements.Add(settlement);
                    added++;
                }
            }

            if (added < pageSettlements.Count || pageSettlements.Count < PageSize)
                return settlements;
        }

        return settlements;
    }

    /// <summary>How many lines or settlements one page asks for (Razorpay's own <c>count</c> parameter).</summary>
    private const int PageSize = 100;

    /// <summary>
    /// One GET against Razorpay's REST API, authorized the way it wants - basic auth with the key id as the
    /// user and the secret as the password. Null when the call could not be made, was refused, or answered
    /// something that is not JSON: every caller here treats a gateway that could not be asked as a day to
    /// report, never as an exception thrown at a screen.
    /// </summary>
    private static async Task<JsonElement?> GetAsync(string keyId, string keySecret, string pathAndQuery)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, ApiRoot + pathAndQuery);
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var response = await Http.SendAsync(message);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(body);

            // Cloned, because the element has to outlive the document it was read from.
            return document.RootElement.Clone();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// A day as Razorpay speaks it - seconds since the epoch, the UTC convention every timestamp in this
    /// system is kept in. A report is asked for as a window, so both ends of the day are named and a reply can
    /// never straddle a midnight the question did not mean to include.
    /// </summary>
    private static long EpochSeconds(DateOnly day, bool endOfDay) =>
        ((DateTimeOffset)day.ToDateTime(
            endOfDay ? new TimeOnly(23, 59, 59) : TimeOnly.MinValue,
            DateTimeKind.Utc)).ToUnixTimeSeconds();

    /// <summary>
    /// THE PULL: reads the settlement window off Razorpay and writes the shop's books up from it - the
    /// settlements themselves (<see cref="Settlement"/>), every line of each day's reconciliation report
    /// (<see cref="SettlementItem"/>), the authoritative gateway charges onto the payment rows they belong to
    /// (<see cref="ApplyReconCharges"/>), and finally the refunds the gateway is still holding
    /// (<see cref="RazorpayRefunds.RefreshPendingRefundsAsync"/>) - one entry point, so everything the gateway
    /// has to say about money is asked for in one place and a scheduler or an admin button has nothing to
    /// sequence.
    ///
    /// Safe to run every hour: the window defaults to yesterday and the seven days before it
    /// (<see cref="ResolvePullDays"/>), which is what a settlement that was created late, put on hold or
    /// corrected after the fact needs; a line is identified by Razorpay's own id of the thing it is plus its
    /// type, so a day pulled twice adds nothing. Nothing is ever deleted - a line the gateway has stopped
    /// reporting stays as it was last said, because it settled money that really moved.
    ///
    /// A gateway that cannot be reached never throws at the caller: a day that could not be read is counted in
    /// <see cref="SettlementSyncOutcome.FailedDays"/> and named in the messages, and the rest of the window is
    /// read anyway. <paramref name="today"/> exists so the window can be reasoned about in a test; the pull
    /// itself works in UTC, like every other timestamp in this system.
    /// </summary>
    public static async Task<SettlementSyncOutcome> SyncAsync(
        HomecutiesDbContext context,
        string keyId,
        string keySecret,
        DateOnly? from = null,
        DateOnly? to = null,
        DateOnly? today = null)
    {
        var messages = new List<string>();
        var now = DateTime.UtcNow;

        if (!IsConfigured(keyId, keySecret))
        {
            messages.Add("Razorpay is not configured, so there was nothing to pull.");

            return new SettlementSyncOutcome(0, 0, 0, 0, 0, 0, 0, 0, messages);
        }

        var window = ResolvePullDays(from, to, today ?? DateOnly.FromDateTime(now));

        var daysPulled = 0;
        var linesStored = 0;
        var paymentsCorrected = 0;
        var paymentsNotFound = 0;
        var amountMismatches = 0;
        var creditMismatches = 0;
        var failedDays = 0;

        // 1. The settlements of the window, written down first: a recon line names the settlement it came in
        //    with, and a line cannot point at one this pull has not stored yet.
        var settlements = new List<Settlement>();
        var listed = await FetchSettlementsAsync(keyId, keySecret, window.From, window.To, now);

        if (listed == null)
        {
            messages.Add($"The settlement list for {window.From:dd MMM} to {window.To:dd MMM} could not be " +
                "read. The days are still read below, and the lines of a settlement that was not listed are " +
                "stored without pointing at it.");
        }
        else
        {
            var ids = listed.Select(s => s.RazorpaySettlementId).ToList();

            settlements = await context.Settlements
                .Where(s => ids.Contains(s.RazorpaySettlementId))
                .ToListAsync();

            foreach (var settlement in listed)
            {
                var known = settlements.FirstOrDefault(s => string.Equals(
                    s.RazorpaySettlementId, settlement.RazorpaySettlementId, StringComparison.OrdinalIgnoreCase));

                if (known == null)
                {
                    context.Settlements.Add(settlement);
                    settlements.Add(settlement);
                }
                else if (CopySettlement(known, settlement))
                {
                    known.UpdatedOn = now;
                }
            }

            // Saved here rather than with everything else at the end: the lines written below point at these
            // settlements, so the rows need the keys the database generates for them first.
            await context.SaveChangesAsync();
        }

        var settlementIds = settlements
            .GroupBy(s => s.RazorpaySettlementId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SettlementId, StringComparer.OrdinalIgnoreCase);

        // 2. One day at a time, oldest first, so the ledger reads in the order the money moved.
        for (var day = window.From; day <= window.To; day = day.AddDays(1))
        {
            var lines = await FetchReconAsync(keyId, keySecret, day);

            if (lines == null)
            {
                failedDays++;
                messages.Add($"{day:dd MMM yyyy}: the reconciliation report could not be read - the day will be " +
                    "asked for again on the next pull.");

                continue;
            }

            daysPulled++;

            if (lines.Count == 0)
                continue;

            // The rows this day's lines already have (a re-pull), read in one go rather than one query per
            // line. The unique index on (entity, type) is what makes this the right question to ask.
            var entityIds = lines.Select(l => l.EntityId).Distinct().ToList();

            var knownItems = await context.SettlementItems
                .Where(i => entityIds.Contains(i.RazorpayEntityId))
                .ToListAsync();

            // The payment rows this day's lines may be about, read once: a line names the payment itself (the
            // entity id on a payment line, Razorpay's payment_id on a refund or adjustment) or the order it was
            // taken against. Asking the database per line would be hundreds of round trips an hour for a pull
            // that normally writes nothing.
            var paymentIds = lines
                .Select(l => l.PaymentBehind)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            var orderIds = lines
                .Select(l => l.OrderId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            var dayPayments = await context.OrderPayments
                .Where(p => (p.RazorpayPaymentId != null && paymentIds.Contains(p.RazorpayPaymentId)) ||
                            (p.RazorpayOrderId != null && orderIds.Contains(p.RazorpayOrderId)))
                .ToListAsync();

            var dayStored = 0;

            foreach (var line in lines)
            {
                var reconDay = day.ToDateTime(TimeOnly.MinValue);

                var settlementId = line.SettlementId != null &&
                                   settlementIds.TryGetValue(line.SettlementId, out var settlement)
                    ? settlement
                    : (long?)null;

                var item = knownItems.FirstOrDefault(i =>
                    string.Equals(i.RazorpayEntityId, line.EntityId, StringComparison.Ordinal) &&
                    string.Equals(i.ItemType, line.ItemType, StringComparison.OrdinalIgnoreCase));

                if (item == null)
                {
                    item = FromReconLine(line, settlementId, reconDay, now);

                    context.SettlementItems.Add(item);
                    knownItems.Add(item);
                    dayStored++;
                }
                else if (ApplyReconLine(item, line, settlementId, reconDay))
                {
                    item.UpdatedOn = now;
                }

                // A settled payment is the authoritative word on what the gateway kept out of it (see
                // ApplyReconCharges) - and the line is the only place the two can be compared.
                if (line.IsPayment)
                {
                    var payment = FindPayment(dayPayments, line);

                    if (payment == null)
                    {
                        paymentsNotFound++;
                    }
                    else
                    {
                        if (AmountDisagrees(payment, line))
                        {
                            amountMismatches++;

                            // Named one by one up to a point: a finding nobody can chase is not a finding.
                            if (amountMismatches <= 10)
                            {
                                messages.Add($"{day:dd MMM yyyy}: settlement line {line.EntityId} carries " +
                                    $"{line.Amount:0.00} but the payment row says {payment.Amount:0.00} - " +
                                    "somebody has to look at that.");
                            }
                        }

                        if (ApplyReconCharges(payment, line))
                        {
                            payment.UpdatedOn = now;
                            paymentsCorrected++;
                        }
                    }
                }

                if (!line.CreditAgreesWithFee)
                    creditMismatches++;
            }

            linesStored += dayStored;

            // The day each settlement went through: the earliest day any of its lines was settled to the bank.
            // It comes from the lines because a settlement of the list does not say it, and it only ever moves
            // BACK - a re-pull must not make the books jump.
            foreach (var group in lines
                .Where(l => l.SettledOn != null && l.SettlementId != null)
                .GroupBy(l => l.SettlementId!, StringComparer.OrdinalIgnoreCase))
            {
                if (!settlementIds.TryGetValue(group.Key, out var settlementId))
                    continue;

                var settledOn = group.Min(l => l.SettledOn);
                var settlement = settlements.FirstOrDefault(s => s.SettlementId == settlementId);

                if (settlement != null && settledOn != null &&
                    (settlement.SettledOn == null || settledOn < settlement.SettledOn))
                {
                    settlement.SettledOn = settledOn;
                    settlement.UpdatedOn = now;
                }
            }
        }

        // 3. Everything the window wrote, in one save: the lines, the payment rows the recon corrected and the
        //    days the settlements went through.
        await context.SaveChangesAsync();

        // 4. The other half of the same question - the money the gateway is still holding on the shop's behalf
        //    (see RazorpayRefunds). A refund travels for a day or two, so a daily pull is exactly when it is
        //    worth asking about.
        var refunds = await RazorpayRefunds.RefreshPendingRefundsAsync(context, keyId, keySecret);

        // 5. What the person reading this has to know, in the order it matters: first the money the shop has no
        //    explanation for, then the arithmetic question the recon data is the only answer to.
        if (paymentsNotFound > 0)
        {
            messages.Add($"{paymentsNotFound} settled payment(s) have no payment row here: a payment taken " +
                "outside the shop (in the Razorpay dashboard, say), or one whose order was never recorded. " +
                "Nothing was invented - only a person can say which order a payment belongs to.");
        }

        if (creditMismatches > 0)
        {
            messages.Add($"{creditMismatches} settlement line(s) carry a credit that is not " +
                "amount - fee - GST. Look at one of those lines: if the fee it carries is the whole deduction " +
                "and the tax is the GST inside that fee, then Razorpay quotes a GST-inclusive fee and the net " +
                "columns here are subtracting the tax a second time. Nothing is guessed about it - both " +
                "spellings are stored, so the answer is in the shop's own settlements.");
        }

        return new SettlementSyncOutcome(
            daysPulled,
            linesStored,
            paymentsCorrected,
            paymentsNotFound,
            amountMismatches,
            creditMismatches,
            refunds,
            failedDays,
            messages);
    }

    /// <summary>
    /// The payment row a settlement line belongs to, out of the rows already read for the day: preferred by the
    /// payment itself (the line's own entity id on a payment line, Razorpay's <c>payment_id</c> on any other
    /// kind), and by the order the gateway says it was taken against when the row predates the payment ids being
    /// kept. The newest attempt of that order is taken, because an order can have several (a retry after a
    /// failure - see <see cref="OrderPayment"/>).
    ///
    /// Null when the shop has no row for it at all. That is a finding rather than a failure (see the pull): a
    /// payment row is not invented here, because which order it belongs to is not something this code can know.
    /// </summary>
    private static OrderPayment? FindPayment(List<OrderPayment> payments, SettlementReconLine line)
    {
        var paymentId = line.PaymentBehind;

        if (!string.IsNullOrEmpty(paymentId))
        {
            var byPayment = payments.FirstOrDefault(p =>
                string.Equals(p.RazorpayPaymentId, paymentId, StringComparison.Ordinal));

            if (byPayment != null)
                return byPayment;
        }

        return string.IsNullOrEmpty(line.OrderId)
            ? null
            : payments
                .Where(p => string.Equals(p.RazorpayOrderId, line.OrderId, StringComparison.Ordinal))
                .OrderByDescending(p => p.PaymentId)
                .FirstOrDefault();
    }

    /// <summary>
    /// A string property of a Razorpay payload, walked down <paramref name="path"/> when more than one name is
    /// given (<c>ReadString(item, "acquirer_data", "arn")</c>). Null when any step is missing or is not a
    /// string: every field of a settlement line is optional as far as the books are concerned, and a field the
    /// gateway has stopped sending has to read as absent rather than as an empty word.
    /// </summary>
    private static string? ReadString(JsonElement element, params string[] path)
    {
        foreach (var property in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out element))
                return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    /// <summary>
    /// A whole number property of a Razorpay payload (amounts are in paise, timestamps in seconds), or null
    /// when it is missing or is not a number - so a figure the gateway did not send is not read as a real 0.
    /// </summary>
    private static int? ReadInt(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>
    /// A flag of a Razorpay payload. Absent counts as false - 'on hold' and 'settled' are things the gateway
    /// has to say out loud, and a report that does not mention them is a report about money that moved.
    /// </summary>
    private static bool ReadBool(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Razorpay's Unix-second timestamp as a UTC DateTime (the convention every timestamp in this system is kept
    /// in), or null when the payload does not carry one.
    /// </summary>
    private static DateTime? ReadTimestamp(JsonElement element, string property)
    {
        var seconds = ReadInt(element, property);

        return seconds.HasValue && seconds.Value > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime
            : null;
    }

    /// <summary>
    /// A gateway string cut to the width of the column it is going into, so a longer answer than the schema
    /// expects cannot fail the whole pull. Null stays null: an absent field is not an empty word.
    /// </summary>
    private static string? Truncate(string? value, int maxLength) =>
        value == null || value.Length <= maxLength ? value : value[..maxLength];
}

