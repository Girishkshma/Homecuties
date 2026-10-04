using System.Linq.Expressions;
using HC.Data.Entities;

namespace HC.Business;

/// <summary>
/// What one product costs the customer, read one way, from every field the product form records on it.
///
/// The form asks for a product's money in two sections - 'Pricing &amp; Charges' (the unit price, both discounts,
/// the packaging / storage / delivery charges and the profit margin) and 'Taxes' (CGST, SGST, IGST) - and a price a
/// shopper is shown has to account for ALL of them, or the card, the cart and the bill describe the same product
/// differently. This class is that one reading:
///
///   * the unit price is the shop's own base for one unit, and the <b>profit margin</b> is ADDED to it (the margin
///     is the shop's declared margin on the goods - the same figure the Finance screen reads with
///     <see cref="OrderMoney.DeclaredProfit"/>);
///   * the <b>packaging, storage and delivery charges</b> are the shop's own handling of one unit, and are added
///     to it as well - and it is that whole figure, the gross, that the discounts come off, so a discount is taken
///     off the price it is advertised on rather than off a part of it;
///   * the <b>discount</b> is a percentage of that gross, and the <b>additional discount</b> is then a percentage of
///     what the first one left - two discounts on one bill, one after the other, which is the only way two
///     percentages of the same bill add up;
///   * the <b>GST</b> falls on what is left (the taxable value): the CGST and SGST rates together where either is
///     set - a sale inside the shop's own state, which is the only reading a price shown to any shopper can make -
///     and the IGST rate where that is the only one recorded;
///   * and the <b>listing price</b> is that taxable value plus that tax: what one unit costs the customer, with
///     nothing left over to surprise them at the checkout. The same price read one discount at a time - the sell
///     price, what the discount leaves of it, and what the additional discount leaves
///     (<see cref="PreDiscountListingPrice"/>, <see cref="PostDiscountListingPrice"/>, <see cref="ListingPrice"/>)
///     - is the walk a product page shows, so a shopper can read where the figure came from rather than take it on
///     trust.
///
/// Every figure on that walk is a whole number of RUPEES, and so is each of the figures it is made of - the gross,
/// both discounts, the taxable value and the tax - because the rupee is the coin this shop takes money in: the price
/// a card shows is the figure the gateway is asked for, and the figures the price is made of add up to it exactly, so
/// a bill read a row at a time agrees with its own total. The parts are rounded BEFORE they are added, one after the
/// other (<see cref="Rupees"/>): the discounts taken off the gross come to the taxable value, and that value with the
/// tax on it comes to the price. Half a rupee goes UP, which is the till's rule and SQL Server's own ROUND, so the
/// two other places this arithmetic is written out - the backfill script, and the database-side reading below - round
/// to the same figure: the script says it with T-SQL's ROUND, and the tree with FLOOR / CEILING arithmetic that EF
/// Core can translate (<see cref="ChargedPriceExpression"/> says why that is not Math.Round).
///
/// It is read by the storefront's listings, the wish list, the cart, the amount the checkout actually asks the
/// gateway for, the amount a retry or a refund is worked out from, and the shop's own books (the per-line money
/// writer and the Finance screen), so none of those can describe the same product differently.
///
/// The class is pure: ten figures in, money out. The only other copy of this arithmetic in the repository is the
/// set-based one in 'HC.Data/Scripts/BackfillOrderItemMoney.sql', which exists to open the books of orders sold
/// before the table existed and says so itself.
/// </summary>
public static class ProductPricing
{
    /// <summary>
    /// The ten figures of one unit, in the order the product form asks for them: the unit price, both discounts,
    /// the profit margin and the three charges ('Pricing &amp; Charges'), then the three tax rates ('Taxes').
    ///
    /// A product and an order line carry exactly these ten fields, and an order line's are the snapshot taken when
    /// it was sold - so a line is priced the way it was priced on the day, whatever the product says now.
    /// </summary>
    public readonly record struct Inputs(
        decimal UnitPrice,
        decimal DiscountPercent,
        decimal AdditionalDiscountPercent,
        decimal ProfitMarginPercent,
        decimal PackagingCharge,
        decimal StorageCharge,
        decimal DeliveryCharge,
        decimal CgstPercent,
        decimal SgstPercent,
        decimal IgstPercent);

    /// <summary>The pricing fields of a product, as the product form holds them.</summary>
    public static Inputs Of(Product product) => new(
        product.UnitPrice,
        product.DiscountPercent,
        product.AdditionalDiscountPercent,
        product.ProfitMarginPercent,
        product.PackagingCharge,
        product.StorageCharge,
        product.DeliveryCharge,
        product.Cgstpercent,
        product.Sgstpercent,
        product.Igstpercent);

    /// <summary>The pricing fields of one order line, as they were snapshotted when the customer bought it.</summary>
    public static Inputs Of(OrderItem line) => new(
        line.UnitPrice,
        line.DiscountPercent,
        line.AdditionalDiscountPercent,
        line.ProfitMarginPercent,
        line.PackagingCharge,
        line.StorageCharge,
        line.DeliveryCharge,
        line.Cgstpercent,
        line.Sgstpercent,
        line.Igstpercent);

    /// <summary>
    /// A figure the customer is charged, rounded to the rupee: money is counted in rupees at this shop, so a price -
    /// and every figure it is made of - is a whole number of them. Half a rupee goes UP (the till's rule, and what
    /// SQL Server's ROUND does), because a shop that rounded a half down would give away money it had already
    /// priced.
    ///
    /// Rounding each figure as it is read, rather than once at the end, is what makes them ADD UP: the discounts
    /// taken off the rounded gross come to the rounded taxable value, and that value with the rounded tax on it comes
    /// to the price. Every step is then a figure a bill could show, and the steps agree with one another.
    /// </summary>
    public static decimal Rupees(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The shop's declared margin on one unit: 'Profit Margin %' of the unit price, and the figure added to the
    /// unit price to make the price the customer pays. It is the same arithmetic the Finance screen reads a sale's
    /// margin with (<see cref="OrderMoney.DeclaredProfit"/>), which is the point - the margin a shop declares and
    /// the margin it charges are one figure.
    /// </summary>
    public static decimal Margin(decimal unitPrice, decimal profitMarginPercent) =>
        unitPrice * profitMarginPercent / 100m;

    /// <summary>The margin on one unit, from its own pricing fields.</summary>
    public static decimal Margin(Inputs price) => Margin(price.UnitPrice, price.ProfitMarginPercent);

    /// <summary>
    /// What the shop's own handling of one unit adds to its price: the packaging, storage and delivery charges,
    /// added up. They are charged on every unit, which is what makes them part of the price rather than a fee the
    /// checkout has to remember to add afterwards.
    /// </summary>
    public static decimal Charges(Inputs price) =>
        price.PackagingCharge + price.StorageCharge + price.DeliveryCharge;

    /// <summary>
    /// The price of one unit before the discounts and before tax: the unit price, plus the shop's margin on it,
    /// plus what the shop's own handling of it costs - a whole number of rupees like every figure the customer is
    /// asked for (<see cref="Rupees"/>), so a discount taken off it is a share of a price rather than of a fraction
    /// of one.
    /// </summary>
    public static decimal Gross(Inputs price) => Rupees(price.UnitPrice + Margin(price) + Charges(price));

    /// <summary>
    /// What the 'Discount %' takes off one unit: a share of the GROSS - the unit price with the shop's margin and
    /// its own handling charges already added to it, which is the price the discount is advertised on - rounded to
    /// the rupee, so what the discount leaves is a price as well.
    /// </summary>
    public static decimal Discount(Inputs price) => Rupees(Gross(price) * price.DiscountPercent / 100m);

    /// <summary>
    /// What the first discount leaves of one unit: the figure the additional discount is taken off. Both figures
    /// are whole rupees, so this one is too.
    /// </summary>
    public static decimal AfterDiscount(Inputs price) => Gross(price) - Discount(price);

    /// <summary>
    /// What the 'Additional Discount %' takes off one unit: a share of what the DISCOUNT left, not of the gross -
    /// the second discount on a bill, taken off the price the first one left - rounded to the rupee like the first.
    /// </summary>
    public static decimal AdditionalDiscount(Inputs price) =>
        Rupees(AfterDiscount(price) * price.AdditionalDiscountPercent / 100m);

    /// <summary>What both discounts take off one unit together.</summary>
    public static decimal TotalDiscount(Inputs price) => Discount(price) + AdditionalDiscount(price);

    /// <summary>
    /// The value the GST is charged on: the gross less the discount and then less the additional discount - what
    /// one unit is really sold for, before tax. It is the gross with the two rounded discounts taken off, so it is
    /// a whole number of rupees, and the two discounts really do come to the difference between it and the gross.
    /// </summary>
    public static decimal TaxableValue(Inputs price) => AfterDiscount(price) - AdditionalDiscount(price);

    /// <summary>
    /// The rate one unit is charged at: the CGST and SGST rates together where either is set (a sale inside the
    /// shop's own state), else the IGST rate (a sale across states). A product recording none of the three is
    /// charged no tax rather than a guessed rate.
    ///
    /// A listing has no customer behind it to say which of the two a sale will be, so the price it shows is the
    /// intra-state one; the IGST rate is read only where it is the ONE rate the product records, which is what the
    /// form's own sections ('CGST / SGST' and 'IGST') expect of it.
    /// </summary>
    public static decimal GstRate(Inputs price) =>
        price.CgstPercent + price.SgstPercent > 0m
            ? price.CgstPercent + price.SgstPercent
            : price.IgstPercent;

    /// <summary>
    /// The GST on one unit: its taxable value at <see cref="GstRate"/>, rounded to the rupee like the value it is
    /// charged on (<see cref="Rupees"/>) - the tax is part of a price, so it is taken in the same coin. The
    /// multiplication itself is OrderMoney.GstOn, which is the one way this shop reads a tax off a value.
    /// </summary>
    public static decimal GstAmount(Inputs price) => TaxOn(TaxableValue(price), GstRate(price));

    /// <summary>
    /// The tax on a value at a rate, rounded to the rupee: the figure a step of the walk below adds to the value it
    /// was charged on.
    /// </summary>
    private static decimal TaxOn(decimal value, decimal gstPercent) =>
        Rupees(OrderMoney.GstOn(value, gstPercent));

    /// <summary>
    /// The listing price: what one unit costs the customer, everything in both sections included - the unit price,
    /// the shop's margin, its packaging / storage / delivery charges, both discounts, and the tax on what is left.
    /// This is the figure the storefront shows, the cart adds up and the checkout asks the gateway for: a whole
    /// number of rupees, and the exact sum of the steps it is made of.
    /// </summary>
    public static decimal ListingPrice(Inputs price) => TaxableValue(price) + GstAmount(price);

    /// <summary>
    /// The same price with both discounts still on it - what one unit would cost a customer without them. It is
    /// what a listing strikes through, so the saving a shopper is shown is the difference between two figures made
    /// up the same way.
    /// </summary>
    public static decimal PreDiscountListingPrice(Inputs price) =>
        Gross(price) + TaxOn(Gross(price), GstRate(price));

    /// <summary>
    /// The same price with only the first discount read - the additional discount still to come: what one unit costs
    /// the customer once the 'Discount %' is off it, with the tax on what that leaves.
    ///
    /// It is the middle figure of the three a screen walks a shopper down - the sell price
    /// (<see cref="PreDiscountListingPrice"/>), this one, and what they actually pay
    /// (<see cref="ListingPrice"/>) - each figure said the same way, the tax on the value left at that step
    /// included, so the three can be read one after the other as a price coming down.
    /// </summary>
    public static decimal PostDiscountListingPrice(Inputs price) =>
        AfterDiscount(price) + TaxOn(AfterDiscount(price), GstRate(price));

    /// <summary>What one order line's unit was charged at - the listing price of the day it was sold.</summary>
    public static decimal ChargedPrice(OrderItem line) => ListingPrice(Of(line));

    /// <summary>
    /// What an order's lines were charged, added up. An order carries one line per physical unit, so this is what
    /// the customer paid for the goods: the figure the gateway was asked for, the figure a retry asks for again,
    /// and the figure a full refund gives back.
    /// </summary>
    public static decimal ChargedTotal(IEnumerable<OrderItem> lines) => lines.Sum(ChargedPrice);

    /// <summary>
    /// <see cref="ChargedPrice"/> as an expression tree, for the queries that have to add an order's money up IN THE
    /// DATABASE - the admin order list and detail, and a customer's lifetime spend - so those can use the one rule
    /// rather than a second copy of it written in SQL.
    ///
    /// EF Core cannot call the method beside it (an expression tree holds no method body), so the composition is
    /// built here out of the line's own columns, figure by figure, by the same arithmetic and in the same order the
    /// C# members above use it in - including the rounding to the rupee (<see cref="Rupees"/>). Every figure is built
    /// ONCE and reused, so a rule that grows - as this one did when the additional discount started coming off what
    /// the discount left, and again when money started being charged in whole rupees - cannot quietly grow a second,
    /// differently-grouped copy of itself beside it. A test pins the two to each other, and pins that EF can
    /// translate this to SQL as an AGGREGATE (see below), which is what makes it a translation rather than a place
    /// for the two to drift apart.
    ///
    /// The rounding is said as FLOOR / CEILING arithmetic rather than as the <see cref="Rupees"/> call itself, and
    /// that is not a stylistic choice: EF Core does NOT translate Math.Round(x, 0, MidpointRounding) at all. It
    /// leaves it to be evaluated on the client, which a query that has to add an order's money up IN THE DATABASE
    /// cannot do - every use of this tree is inside a Sum, and those throw rather than answer. The two-argument
    /// Math.Round(x, 0) does translate, to SQL Server's ROUND(x, 0), but its two meanings differ: SQL Server rounds
    /// half away from zero and .NET rounds half to even, so the same query would send a 12.50 figure down to 12 in
    /// the database and a compiled reading of it up to 13. Taking the halves apart - up on the positive side with
    /// FLOOR(x + 0.5), away on the negative side with CEILING(x - 0.5) - is the same half-away-from-zero rule in both
    /// languages, and FLOOR and CEILING are translated.
    /// </summary>
    public static readonly Expression<Func<OrderItem, decimal>> ChargedPriceExpression = ChargedPriceOf();

    /// <summary>
    /// Builds <see cref="ChargedPriceExpression"/>, naming each of the figures once and composing them in the order
    /// the members above do: the gross, the discount off it, the additional discount off what that left, and the tax
    /// on what is left of that - each one rounded to the rupee as it is composed, by the same rule the members above
    /// round it by (see the note on <see cref="ChargedPriceExpression"/> for why that rule is written as FLOOR /
    /// CEILING here).
    /// </summary>
    private static Expression<Func<OrderItem, decimal>> ChargedPriceOf()
    {
        var line = Expression.Parameter(typeof(OrderItem), "line");

        Expression Column(string name) => Expression.Property(line, name);
        Expression Fixed(decimal value) => Expression.Constant(value, typeof(decimal));

        // A share of a figure: the same 'x * percent / 100m' the C# members above write.
        Expression Share(Expression of, Expression percent) =>
            Expression.Divide(Expression.Multiply(of, percent), Fixed(100m));

        // A function of one decimal, the way the C# members above call it.
        Expression Of(string method, Expression of) =>
            Expression.Call(typeof(Math).GetMethod(method, new[] { typeof(decimal) })!, of);

        // A figure to the rupee, the way Rupees rounds - half a rupee going UP, away from zero - written as the
        // arithmetic EF Core DOES translate (see the note on ChargedPriceExpression):
        //
        //     x >= 0 ? FLOOR(x + 0.5) : CEILING(x - 0.5)
        //
        // Both are exact on a decimal in SQL and in C#, so a compiled reading of this tree comes to the same rupee as
        // ProductPricing.Rupees does, including on an exact half: 12.50 is 13, and -12.50 is -13.
        Expression Rupees(Expression of) => Expression.Condition(
            Expression.GreaterThanOrEqual(of, Fixed(0m)),
            Of(nameof(Math.Floor), Expression.Add(of, Fixed(0.5m))),
            Of(nameof(Math.Ceiling), Expression.Subtract(of, Fixed(0.5m))));

        // The unit price, plus the shop's margin on it, plus its packaging / storage / delivery charges: the gross.
        var unitPrice = Column(nameof(OrderItem.UnitPrice));
        var gross = Rupees(Expression.Add(
            Expression.Add(unitPrice, Share(unitPrice, Column(nameof(OrderItem.ProfitMarginPercent)))),
            Expression.Add(
                Expression.Add(Column(nameof(OrderItem.PackagingCharge)), Column(nameof(OrderItem.StorageCharge))),
                Column(nameof(OrderItem.DeliveryCharge)))));

        // The discount comes off the gross, and the additional discount off what that left: what the tax is charged
        // on, and the figure the whole price is built on.
        var afterDiscount = Expression.Subtract(gross, Rupees(Share(gross, Column(nameof(OrderItem.DiscountPercent)))));

        var taxableValue = Expression.Subtract(
            afterDiscount,
            Rupees(Share(afterDiscount, Column(nameof(OrderItem.AdditionalDiscountPercent)))));

        // The rate the line was charged at: the CGST and SGST rates together where either is set, else IGST.
        var cgst = Column(nameof(OrderItem.Cgstpercent));
        var sgst = Column(nameof(OrderItem.Sgstpercent));
        var rate = Expression.Condition(
            Expression.GreaterThan(Expression.Add(cgst, sgst), Fixed(0m)),
            Expression.Add(cgst, sgst),
            Column(nameof(OrderItem.Igstpercent)));

        // The tax falls on that value, and the price is that value with the tax on it added.
        return Expression.Lambda<Func<OrderItem, decimal>>(
            Expression.Add(taxableValue, Rupees(Share(taxableValue, rate))),
            line);
    }
}
