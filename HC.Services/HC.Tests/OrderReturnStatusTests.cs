using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// Where a return has got to, and what may follow each step - the same rules the admin card offers its
/// buttons on and the service refuses everything else with.
/// </summary>
public class OrderReturnStatusTests
{
    /// <summary>
    /// The open statuses must match the filter of IX_OrderReturns_OrderID_Open in
    /// CreateOrderReturnsTable.sql. If they ever drift apart, 'one open return per order' would be enforced
    /// by the database on different rows than the service calls open - and one parcel could be refunded
    /// twice.
    /// </summary>
    [Fact]
    public void TheOpenStatusesAreTheOnesTheDatabaseFiltersOn()
    {
        Assert.Equal(new[] { "Requested", "Arranged", "Received" }, OrderReturnStatus.Open);

        Assert.True(OrderReturnStatus.IsOpen(OrderReturnStatus.Requested));
        Assert.True(OrderReturnStatus.IsOpen(OrderReturnStatus.Arranged));
        Assert.True(OrderReturnStatus.IsOpen(OrderReturnStatus.Received));

        Assert.False(OrderReturnStatus.IsOpen(OrderReturnStatus.Closed));
        Assert.False(OrderReturnStatus.IsOpen(OrderReturnStatus.Rejected));
        Assert.False(OrderReturnStatus.IsOpen(OrderReturnStatus.Withdrawn));
        Assert.False(OrderReturnStatus.IsOpen(""));
    }

    /// <summary>
    /// An ask is only answered (or taken back) while nobody has answered it: a second answer is how one
    /// parcel becomes two refunds, so the service refuses it and the card stops offering the buttons.
    /// </summary>
    [Theory]
    [InlineData("Requested", true)]
    [InlineData("Arranged", false)]
    [InlineData("Received", false)]
    [InlineData("Closed", false)]
    [InlineData("Rejected", false)]
    [InlineData("Withdrawn", false)]
    public void OnlyAnUnansweredAskCanBeAnsweredOrTakenBack(string statusId, bool expected)
    {
        Assert.Equal(expected, OrderReturnStatus.CanDecide(statusId));
        Assert.Equal(expected, OrderReturnStatus.CanWithdraw(statusId));
    }

    /// <summary>'Approved' is read off the status rather than stored a second time (see DecisionOn/By/Comment).</summary>
    [Fact]
    public void ApprovedIsReadOffTheStatus()
    {
        Assert.True(OrderReturnStatus.WasApproved(OrderReturnStatus.Arranged));
        Assert.True(OrderReturnStatus.WasApproved(OrderReturnStatus.Received));
        Assert.True(OrderReturnStatus.WasApproved(OrderReturnStatus.Closed));

        Assert.False(OrderReturnStatus.WasApproved(OrderReturnStatus.Requested));
        Assert.False(OrderReturnStatus.WasApproved(OrderReturnStatus.Rejected));
        Assert.False(OrderReturnStatus.WasApproved(OrderReturnStatus.Withdrawn));
    }

    /// <summary>
    /// The parcel is booked back in by hand, looked over, and closed - in that order and no other. The inspection
    /// only exists while the parcel is in the shop: after the close its units are on sale again, and writing one
    /// off then would take a unit off a shelf it may already have been sold from.
    /// </summary>
    [Fact]
    public void TheParcelIsReceivedBeforeItCanBeLookedOverAndClosed()
    {
        Assert.True(OrderReturnStatus.CanMarkReceived(OrderReturnStatus.Arranged));
        Assert.True(OrderReturnStatus.CanInspect(OrderReturnStatus.Received));
        Assert.True(OrderReturnStatus.CanClose(OrderReturnStatus.Received));

        Assert.False(OrderReturnStatus.CanMarkReceived(OrderReturnStatus.Requested));
        Assert.False(OrderReturnStatus.CanMarkReceived(OrderReturnStatus.Received));
        Assert.False(OrderReturnStatus.CanInspect(OrderReturnStatus.Requested));
        Assert.False(OrderReturnStatus.CanInspect(OrderReturnStatus.Arranged));
        Assert.False(OrderReturnStatus.CanInspect(OrderReturnStatus.Closed));
        Assert.False(OrderReturnStatus.CanInspect(OrderReturnStatus.Rejected));
        Assert.False(OrderReturnStatus.CanClose(OrderReturnStatus.Requested));
        Assert.False(OrderReturnStatus.CanClose(OrderReturnStatus.Arranged));
        Assert.False(OrderReturnStatus.CanClose(OrderReturnStatus.Closed));
    }

    /// <summary>
    /// The parcel is booked back in by hand, and the return is closed, in that order and no other.
    /// </summary>
    [Fact]
    public void TheParcelIsReceivedBeforeTheReturnCanBeClosed()
    {
        Assert.True(OrderReturnStatus.CanMarkReceived(OrderReturnStatus.Arranged));
        Assert.True(OrderReturnStatus.CanClose(OrderReturnStatus.Received));

        Assert.False(OrderReturnStatus.CanMarkReceived(OrderReturnStatus.Requested));
        Assert.False(OrderReturnStatus.CanMarkReceived(OrderReturnStatus.Received));
        Assert.False(OrderReturnStatus.CanClose(OrderReturnStatus.Requested));
        Assert.False(OrderReturnStatus.CanClose(OrderReturnStatus.Arranged));
        Assert.False(OrderReturnStatus.CanClose(OrderReturnStatus.Closed));
    }

    /// <summary>
    /// Every status the vocabulary names is one the database can hold, and each one is a word rather than a
    /// number: OrderReturns.Status is a varchar(20) the screens read as it stands.
    /// </summary>
    [Fact]
    public void EveryStatusFitsTheColumnItIsStoredIn()
    {
        string[] all =
        {
            OrderReturnStatus.Requested, OrderReturnStatus.Arranged, OrderReturnStatus.Received,
            OrderReturnStatus.Closed, OrderReturnStatus.Rejected, OrderReturnStatus.Withdrawn
        };

        foreach (var status in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(status));
            Assert.True(status.Length <= 20, $"'{status}' does not fit OrderReturns.Status varchar(20).");
        }

        Assert.Equal(all.Length, all.Distinct().Count());
    }
}
