using HC.Business;
using HC.Data.Entities;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The words a return is described with - why it was asked for, where the ask came from, and which leg of
/// the order a parcel is - and the checks that keep a caller from writing something the screens never
/// offer.
/// </summary>
public class OrderReturnVocabularyTests
{
    /// <summary>
    /// The five reasons the picker offers are the only ones a customer's ask may carry, and the courier's
    /// two are a different set: an undelivered parcel's ask can never be written as something the customer
    /// would have said.
    /// </summary>
    [Fact]
    public void TheCustomersReasonsAndTheCouriersAreDifferentSets()
    {
        Assert.Equal(
            new[] { "NotNeeded", "WrongItem", "Damaged", "NotAsDescribed", "Other" },
            OrderReturnReason.CustomerReasons);

        foreach (var reason in OrderReturnReason.CustomerReasons)
        {
            Assert.True(OrderReturnReason.IsCustomerReason(reason));
            Assert.False(OrderReturnReason.IsCourierReason(reason));
        }

        Assert.True(OrderReturnReason.IsCourierReason(OrderReturnReason.RefusedAtDoor));
        Assert.True(OrderReturnReason.IsCourierReason(OrderReturnReason.ReturnedToOrigin));
        Assert.False(OrderReturnReason.IsCustomerReason(OrderReturnReason.RefusedAtDoor));
        Assert.False(OrderReturnReason.IsCustomerReason(OrderReturnReason.ReturnedToOrigin));
    }

    /// <summary>A code nobody offered, and no reason at all, are both refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notneeded")]
    [InlineData("SomethingElse")]
    public void AnUnknownOrMissingReasonIsRefused(string? reasonCode)
    {
        Assert.False(OrderReturnReason.IsCustomerReason(reasonCode));
        Assert.False(OrderReturnReason.IsCourierReason(reasonCode));
    }

    /// <summary>
    /// Every reason the server can write has wording of its own, and an unknown code falls back rather than
    /// throwing - the admin screen renders what it is given.
    /// </summary>
    [Fact]
    public void EveryReasonHasItsOwnWording()
    {
        var written = new List<string>();

        foreach (var reason in OrderReturnReason.CustomerReasons
                     .Concat(new[] { OrderReturnReason.RefusedAtDoor, OrderReturnReason.ReturnedToOrigin }))
        {
            var label = OrderReturnReason.Label(reason);

            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.DoesNotContain(label, written);

            written.Add(label);
        }

        Assert.Equal("Return", OrderReturnReason.Label("SomethingElse"));
        Assert.Equal("Return", OrderReturnReason.Label(null));
    }

    /// <summary>
    /// A courier's report reads as something nobody at the shop asked for - which is what the admin card
    /// says about where the ask came from.
    /// </summary>
    [Fact]
    public void TheTwoOriginsReadDifferently()
    {
        Assert.Equal("Requested by the customer", OrderReturnOrigin.Label(OrderReturnOrigin.Customer));
        Assert.Equal("Reported by the courier", OrderReturnOrigin.Label(OrderReturnOrigin.Courier));
        Assert.Equal("Return", OrderReturnOrigin.Label(null));
    }

    /// <summary>
    /// An order carries one parcel going out and one coming back, and a caller that says nothing means the
    /// one that went out - every row written before a return had a leg is one.
    /// </summary>
    [Theory]
    [InlineData(null, "Forward")]
    [InlineData("", "Forward")]
    [InlineData("   ", "Forward")]
    [InlineData("Forward", "Forward")]
    [InlineData("forward", "Forward")]
    [InlineData(" Reverse ", "Reverse")]
    [InlineData("reverse", "Reverse")]
    [InlineData("sideways", "Forward")]
    public void AnAbsentDirectionIsTheParcelThatWentOut(string? direction, string expected)
    {
        Assert.Equal(expected, OrderShipment.NormaliseDirection(direction));
    }

    /// <summary>
    /// A direction that is not one of the two is a typo, and the caller is told so rather than having a
    /// return's pickup quietly recorded as the delivery.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("Forward", true)]
    [InlineData("Reverse", true)]
    [InlineData(" reverse ", true)]
    [InlineData("sideways", false)]
    [InlineData("Back", false)]
    public void OnlyTheTwoLegsAreAccepted(string? direction, bool expected)
    {
        Assert.Equal(expected, OrderShipment.IsValidDirection(direction));
    }

    /// <summary>
    /// The provider ids and the two directions are what the columns hold: a value longer than the column
    /// would be truncated by the database and matched by nothing.
    /// </summary>
    [Fact]
    public void TheStoredValuesFitTheirColumns()
    {
        Assert.True(OrderShipment.DirectionForward.Length <= 10);
        Assert.True(OrderShipment.DirectionReverse.Length <= 10);
        Assert.Equal("Forward", OrderShipment.DirectionForward);
        Assert.Equal("Reverse", OrderShipment.DirectionReverse);
    }
}
