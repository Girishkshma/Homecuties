namespace HC.Adapters.Messaging;

/// <summary>
/// One way for the shop to send a text message - today Twilio, behind this shape so the rest of the
/// app never names the provider.
///
/// Everything provider-specific lives behind this interface: the account, the sign-in, the sender
/// the message goes out from, the field names of the answer and Twilio's error codes. A caller works
/// with <see cref="SmsSendResult"/> and the shop's own wording, so a second SMS provider is added by
/// writing one more adapter and registering it - no caller, controller or rule has to change.
///
/// Two rules the adapter keeps, both deliberate:
///
///  * A send NEVER throws. A failed one comes back as <see cref="SmsSendResult.Succeeded"/> false
///    with a sentence for the person waiting, the same rule <c>IShipmentProvider</c> and
///    <c>IPincodeService</c> follow: a customer's page must not break because a text could not go
///    out. Only a cancellation the caller itself asked for is passed on as an exception.
///
///  * It decides nothing about the shop. It reports whether Twilio accepted the message for
///    delivery; what that means for an order, an appointment or a customer is the caller's decision.
///
/// <see cref="SmsSendResult.Succeeded"/> means Twilio ACCEPTED the message - not that it reached the
/// handset. Delivery reports come later, through Twilio's status callbacks, and are not wired here.
/// </summary>
public interface ISmsSender
{
    /// <summary>
    /// False when the adapter is missing what it needs (the credentials of its own configuration
    /// section and a sender - 'Twilio:AccountSid', 'Twilio:AuthToken' and either 'Twilio:FromNumber'
    /// or 'Twilio:MessagingServiceSid') or was switched off. A send through such an adapter is never
    /// sent anywhere: it comes back with <see cref="SmsSendResult.Succeeded"/> false and one sentence
    /// for the person waiting, exactly like any other failed send.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Who this adapter is, whether it is usable and where it sends from - shown in the startup log
    /// and on any screen that offers to text a customer. Never includes credentials.
    /// </summary>
    SmsSenderInfo Describe();

    /// <summary>
    /// Sends one text message to <paramref name="to"/>, or explains why it could not be sent. Never
    /// throws; an empty <paramref name="body"/> and a number that is not a real one are refused here
    /// rather than paid for by a round trip to Twilio.
    /// </summary>
    /// <param name="to">
    /// The mobile number, in any shape a person types (e.g. '+91 98458 61860' or '9845861860'). A
    /// number without a country code is read in the configured default country (see
    /// <see cref="SmsPhoneNumber"/>); Twilio is only ever called with the E.164 form.
    /// </param>
    /// <param name="body">The text of the message (trimmed; at most 1600 characters).</param>
    Task<SmsSendResult> SendAsync(string to, string body, CancellationToken cancellationToken = default);
}
