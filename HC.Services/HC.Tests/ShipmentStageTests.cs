using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What the courier's own wording means, what it may do to an order, and what it asks for: the one mapping both
/// 'My Orders' and the admin order screen read (see ShipmentStatusFlow). The wording is the contract - the numbers
/// the provider sends beside it are its own business and are never read here.
/// </summary>
public class ShipmentStageTests
{
    /// <summary>
    /// The wordings a courier uses for the parcel's journey, in the order they have to be read: 'RTO Delivered'
    /// is coming back to us rather than arriving at the customer's, and a refusal sits before the failed attempt
    /// it is often reported as ('Undelivered - Refused').
    /// </summary>
    [Theory]
    [InlineData("RTO Initiated", ShipmentStage.Rto)]
    [InlineData("return to origin", ShipmentStage.Rto)]
    [InlineData("RTO Delivered", ShipmentStage.Rto)]
    [InlineData("Canceled", ShipmentStage.Cancelled)]
    [InlineData("Refused", ShipmentStage.Refused)]
    [InlineData("Refused at door", ShipmentStage.Refused)]
    [InlineData("Undelivered - Refused", ShipmentStage.Refused)]
    [InlineData("Consignee Refused", ShipmentStage.Refused)]
    [InlineData("Undelivered", ShipmentStage.Undelivered)]
    [InlineData("Not Delivered", ShipmentStage.Undelivered)]
    [InlineData("Out for Delivery", ShipmentStage.OutForDelivery)]
    [InlineData("Delivered", ShipmentStage.Delivered)]
    [InlineData("In Transit", ShipmentStage.InTransit)]
    [InlineData("Picked Up", ShipmentStage.InTransit)]
    [InlineData("AWB Assigned", ShipmentStage.Booked)]
    [InlineData("Pickup Scheduled", ShipmentStage.Booked)]
    [InlineData("something new the courier invented", ShipmentStage.Unknown)]
    [InlineData("", ShipmentStage.Unknown)]
    [InlineData(null, ShipmentStage.Unknown)]
    public void TheCouriersWordingMeansOneStage(string? wording, ShipmentStage expected)
    {
        Assert.Equal(expected, ShipmentStatusFlow.FromProviderText(wording));
    }

    /// <summary>
    /// A refusal at the door is the customer's own. A PICKUP being refused is not: that parcel never left the
    /// shop, so it is still a booked parcel and no return is asked for it (see
    /// <see cref="ShipmentStatusFlow.ReturnReasonFor"/>).
    /// </summary>
    [Theory]
    [InlineData("Pickup Refused", ShipmentStage.Booked)]
    [InlineData("Pickup Error", ShipmentStage.Booked)]
    [InlineData("Pickup Rescheduled", ShipmentStage.Booked)]
    public void ARefusedPickupIsNotARefusedDelivery(string wording, ShipmentStage expected)
    {
        Assert.Equal(expected, ShipmentStatusFlow.FromProviderText(wording));
        Assert.Null(ShipmentStatusFlow.ReturnReasonFor(ShipmentStatusFlow.FromProviderText(wording)));
    }

    /// <summary>
    /// The two stages that mean the parcel is coming back are the only ones that ask for a return - and that ask
    /// is a courier's own, so the shop team answers it rather than the courier deciding it. A failed attempt the
    /// courier will try again asks for nothing: nothing has come back yet.
    /// </summary>
    [Fact]
    public void OnlyComingBackAsksForAReturn()
    {
        Assert.Equal(
            OrderReturnReason.RefusedAtDoor,
            ShipmentStatusFlow.ReturnReasonFor(ShipmentStage.Refused));

        Assert.Equal(
            OrderReturnReason.ReturnedToOrigin,
            ShipmentStatusFlow.ReturnReasonFor(ShipmentStage.Rto));

        Assert.True(OrderReturnReason.IsCourierReason(ShipmentStatusFlow.ReturnReasonFor(ShipmentStage.Refused)));
        Assert.True(OrderReturnReason.IsCourierReason(ShipmentStatusFlow.ReturnReasonFor(ShipmentStage.Rto)));

        foreach (var stage in new[]
                 {
                     ShipmentStage.Unknown, ShipmentStage.Booked, ShipmentStage.InTransit,
                     ShipmentStage.OutForDelivery, ShipmentStage.Delivered, ShipmentStage.Undelivered,
                     ShipmentStage.Cancelled
                 })
        {
            Assert.Null(ShipmentStatusFlow.ReturnReasonFor(stage));
        }
    }

    /// <summary>
    /// A courier status only ever advances an order. A refusal and an RTO move it nowhere: they raise an ask for
    /// the shop team, who decide - so a parcel that came back can never write an order off, and never starts a
    /// refund, on its own.
    /// </summary>
    [Fact]
    public void ACourierStatusNeverWritesAnOrderOff()
    {
        Assert.Null(ShipmentStatusFlow.OrderStatusFor(ShipmentStage.Refused));
        Assert.Null(ShipmentStatusFlow.OrderStatusFor(ShipmentStage.Rto));
        Assert.Null(ShipmentStatusFlow.OrderStatusFor(ShipmentStage.Undelivered));
        Assert.Null(ShipmentStatusFlow.OrderStatusFor(ShipmentStage.Booked));
        Assert.Null(ShipmentStatusFlow.OrderStatusFor(ShipmentStage.Cancelled));

        Assert.Equal(OrderStatusFlow.Shipped, ShipmentStatusFlow.OrderStatusFor(ShipmentStage.InTransit));
        Assert.Equal(OrderStatusFlow.Shipped, ShipmentStatusFlow.OrderStatusFor(ShipmentStage.OutForDelivery));
        Assert.Equal(OrderStatusFlow.Delivered, ShipmentStatusFlow.OrderStatusFor(ShipmentStage.Delivered));
    }

    /// <summary>
    /// The pull stops asking about a parcel once there is nothing left to learn: it arrived, the customer refused
    /// it, it is on its way back, or the pickup was called off. A failed attempt is deliberately NOT the end - the
    /// courier tries again, and that next attempt is exactly what 'My Orders' waits for.
    /// </summary>
    [Fact]
    public void SomeEndsStopThePullAndOneDoesNot()
    {
        Assert.True(ShipmentStatusFlow.IsClosed(ShipmentStage.Delivered));
        Assert.True(ShipmentStatusFlow.IsClosed(ShipmentStage.Refused));
        Assert.True(ShipmentStatusFlow.IsClosed(ShipmentStage.Rto));
        Assert.True(ShipmentStatusFlow.IsClosed(ShipmentStage.Cancelled));

        Assert.False(ShipmentStatusFlow.IsClosed(ShipmentStage.Undelivered));
        Assert.False(ShipmentStatusFlow.IsClosed(ShipmentStage.OutForDelivery));
        Assert.False(ShipmentStatusFlow.IsClosed(ShipmentStage.InTransit));
        Assert.False(ShipmentStatusFlow.IsClosed(ShipmentStage.Booked));
        Assert.False(ShipmentStatusFlow.IsClosed(ShipmentStage.Unknown));
    }

    /// <summary>
    /// Every stage the screens can show has the shop's own wording except 'Unknown', which deliberately says
    /// nothing so the courier's own sentence is shown instead of a guess.
    /// </summary>
    [Fact]
    public void EveryStageHasWordingExceptTheOneThatMeansNothing()
    {
        var wordings = new List<string>();

        foreach (var stage in Enum.GetValues<ShipmentStage>())
        {
            var wording = ShipmentStatusFlow.Describe(stage);

            if (stage == ShipmentStage.Unknown)
            {
                Assert.Equal(string.Empty, wording);
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(wording));
            Assert.DoesNotContain(wording, wordings);

            wordings.Add(wording);
        }

        Assert.Equal(Enum.GetValues<ShipmentStage>().Length - 1, wordings.Count);
    }

    /// <summary>
    /// A parcel the shop carries itself has no courier to report on it, so only the two moves the shop team can
    /// make are reflected on it: dispatched puts it on the way, delivered ends it. A refusal is not something the
    /// shop's own delivery can be told about - the mapping would have to lie to say otherwise.
    /// </summary>
    [Fact]
    public void TheShopsOwnDeliveryOnlyMirrorsItsTwoMoves()
    {
        Assert.Equal(ShipmentStage.InTransit, ShipmentStatusFlow.OwnDeliveryStageFor(OrderStatusFlow.Shipped));
        Assert.Equal(ShipmentStage.Delivered, ShipmentStatusFlow.OwnDeliveryStageFor(OrderStatusFlow.Delivered));

        Assert.Null(ShipmentStatusFlow.OwnDeliveryStageFor(OrderStatusFlow.Confirmed));
        Assert.Null(ShipmentStatusFlow.OwnDeliveryStageFor(OrderStatusFlow.Cancelled));
        Assert.Null(ShipmentStatusFlow.OwnDeliveryStageFor(OrderStatusFlow.Returned));
    }

    /// <summary>
    /// The two stages of a parcel the shop carries itself are written down as text and read back out of it, so
    /// such a parcel keeps meaning what it was written as (see ShipmentStatusFlow.OwnDeliveryText). A stage that
    /// has no such wording writes nothing at all, which is what keeps the Shipped move safe.
    /// </summary>
    [Fact]
    public void AShopCarriedParcelReadsBackAsTheStageItWasWrittenAs()
    {
        foreach (var stage in new[] { ShipmentStage.InTransit, ShipmentStage.Delivered })
        {
            Assert.Equal(stage, ShipmentStatusFlow.FromProviderText(ShipmentStatusFlow.OwnDeliveryText(stage)));
        }

        Assert.Equal(string.Empty, ShipmentStatusFlow.OwnDeliveryText(ShipmentStage.Refused));
        Assert.Equal(string.Empty, ShipmentStatusFlow.OwnDeliveryText(ShipmentStage.Undelivered));
    }
}
