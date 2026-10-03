namespace HC.Business.Dtos;

/// <summary>
/// One place a PIN code covers - the area the customer picks, and the city and state that come with it.
///
/// An Indian PIN code is not one place but a group of post offices, which is why a lookup answers with
/// a list of these: '600001' alone covers seven of them. The public India Post directory knows post
/// offices rather than customer streets, so <see cref="Area"/> is a post office name ("Sowcarpet") and
/// <see cref="City"/> is the district that post office sits in - the closest thing the directory has
/// to a city.
/// </summary>
public class PincodeAreaDto
{
    public string Area { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";

    /// <summary>The directory's words for the office: "Head Post Office", "Sub Post Office", "Branch Post Office".</summary>
    public string BranchType { get; set; } = "";

    /// <summary>
    /// Whether mail for this PIN code is delivered from here.
    ///
    /// The directory lists every post office a PIN covers, not only the ones that deliver - a sorting
    /// office or a hospital's counter is listed too - so a customer's own locality is often a
    /// non-delivering office ("Sowcarpet" serves 600001 but does not deliver it). Saying which office
    /// does deliver is therefore what tells the customer which choice to make.
    /// </summary>
    public bool Delivers { get; set; }
}

/// <summary>
/// Answer to 'Customer/GetPincode/{pincode}': what the storefront can fill in for the customer instead
/// of asking them to type the city, the state and the area.
///
/// <see cref="ResultDto.Result"/> is 0 - with <see cref="Areas"/> empty and a reason in
/// <see cref="ResultDto.Messages"/> - when the value is not a whole PIN code, is not in the directory,
/// or the directory could not be reached. None of those may stop a customer from ordering, so the
/// storefront says what happened and lets them type the address themselves.
/// </summary>
public class PincodeLookupResultDto : ResultDto
{
    public string Pincode { get; set; } = "";

    /// <summary>
    /// Every place the PIN covers, as the public directory lists them. One PIN usually covers several post
    /// offices, so the storefront offers the choice rather than guessing which one the customer means; the
    /// office that delivers comes first, and <see cref="PincodeAreaDto.Delivers"/> marks it.
    /// </summary>
    public List<PincodeAreaDto> Areas { get; set; } = new();
}
