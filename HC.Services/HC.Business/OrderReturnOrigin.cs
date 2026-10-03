namespace HC.Business;

/// <summary>
/// Which way a return was asked for - OrderReturns.Origin, i.e. who raised the ask.
///
/// The customer can only ask on an order that has been delivered to them
/// (see <see cref="OrderStatusFlow.CanCustomerReturn"/>), because until then nothing is theirs to send
/// back. The courier raises the other kind by itself: a parcel refused at the doorstep or one that
/// could not be delivered and is coming back says so in its tracking, and the tracking pull writes the
/// ask down (OrderReturnReason.RefusedAtDoor / ReturnedToOrigin).
///
/// A courier ask is only ever an ASK: it is the shop team that decides (<see cref="OrderReturnStatus"/>),
/// so an undelivered parcel never turns into a refund on its own.
/// </summary>
public static class OrderReturnOrigin
{
    /// <summary>The customer asked, from 'My Orders', for a delivered order.</summary>
    public const string Customer = "Customer";

    /// <summary>The courier's own report raised it (a refusal, or a parcel coming back undelivered).</summary>
    public const string Courier = "Courier";

    /// <summary>
    /// What a screen says about where an ask came from. It is here rather than on each screen so the admin
    /// area and 'My Orders' describe the same event the same way (a courier-raised return has to read
    /// differently: nobody at the shop asked for it and no customer wrote a reason).
    /// </summary>
    public static string Label(string? origin) => origin switch
    {
        Courier => "Reported by the courier",
        Customer => "Requested by the customer",
        _ => "Return"
    };
}
