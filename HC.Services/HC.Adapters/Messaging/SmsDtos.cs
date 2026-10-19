namespace HC.Adapters.Messaging;

/// <summary>
/// What one send came back with: whether Twilio accepted the message, its SID for support, and - when
/// it did not - a sentence for the person waiting. It is a snapshot of one call: no database, no
/// customer, no order.
///
/// <see cref="Succeeded"/> means the message was accepted for delivery, not that it has been
/// delivered: Twilio reports the handset's fate later, through a status callback this adapter does
/// not subscribe to. A caller that needs a delivery guarantee needs more than an SMS.
/// </summary>
public class SmsSendResult
{
    /// <summary>True when Twilio accepted the message for delivery.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Twilio's id for the message ('SM...'), or "" when nothing was sent. Support uses it.</summary>
    public string MessageSid { get; set; } = string.Empty;

    /// <summary>Twilio's status at the moment it answered (e.g. 'queued'), or "" when nothing was sent.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The number the message was addressed to, in E.164 form (as far as it could be read).</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>Why the send failed, in words for the person waiting ("" when it succeeded).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Twilio's own error code when it refused the message (e.g. 21608, an unverified number on a
    /// trial account), for support. Null when the failure happened before Twilio was asked or when
    /// Twilio gave no code.
    /// </summary>
    public int? ErrorCode { get; set; }

    /// <summary>The shape a send Twilio accepted answers with.</summary>
    public static SmsSendResult Sent(string to, string messageSid, string status) => new()
    {
        Succeeded = true,
        To = to,
        MessageSid = messageSid,
        Status = status
    };

    /// <summary>
    /// The same shape every failed send answers with. <paramref name="to"/> is echoed back so a caller
    /// with several messages in flight can never mix two of them up.
    /// </summary>
    public static SmsSendResult Failed(string to, string message, int? errorCode = null) => new()
    {
        Succeeded = false,
        To = to,
        Message = message,
        ErrorCode = errorCode
    };
}

/// <summary>
/// One SMS adapter as the startup log and the shop's screens see it: which adapter it is, whether it
/// is ready to be used and where a message goes out from. The credentials themselves are never part
/// of this shape - only whether they are present (<see cref="Configured"/>).
/// </summary>
public class SmsSenderInfo
{
    /// <summary>The adapter's stable id (e.g. "Twilio"), for logs and screens.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What the shop team reads in a list of senders.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>True when the adapter has everything it needs and is not switched off.</summary>
    public bool Configured { get; set; }

    /// <summary>
    /// The Twilio number texts go out from (e.g. '+17372508034'), or "" when a Messaging Service is
    /// used instead - Twilio then picks the sender from that service. Never a credential.
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// True when sends go through a Twilio Messaging Service ('Twilio:MessagingServiceSid') rather
    /// than a fixed 'Twilio:FromNumber'. The SID itself is not reported here.
    /// </summary>
    public bool UsesMessagingService { get; set; }

    /// <summary>
    /// The country a bare mobile number is read in (e.g. '91' for India), as configured. It is what
    /// turns a number typed as '9845861860' into the '+919845861860' Twilio needs.
    /// </summary>
    public string DefaultCountryCode { get; set; } = string.Empty;
}
