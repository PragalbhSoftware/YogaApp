namespace YogaMarketplace.Web.Copy;

/// <summary>
/// User-facing labels. Yoga is the first category and Mumbai the first city;
/// swap these strings (or move them to resources) without changing the page flow.
/// </summary>
public static class UiCopy
{
    public const string AppName = "Yoga Marketplace";
    public const string CityName = "Mumbai";
    public const string CategoryName = "Yoga";
    public const string ProviderSingular = "Instructor";
    public const string ProviderPlural = "Instructors";

    public const string Tagline = "Yoga in Mumbai";
    public const string SkipToContent = "Skip to content";
    public const string SignOut = "Sign out";
    public const string AreaNav = "Area";
    public const string FooterNote = "Mumbai · Yoga";

    public const string HomeTitle = "Find a yoga instructor";
    public const string HomeLead = "Sign in with your phone, choose a Mumbai area, then browse verified instructors for home, studio, or online sessions.";
    public const string SignInCta = "Sign in with phone";
    public const string Continue = "Continue";

    public const string SignInTitle = "Account";
    public const string SignInLead = "New customers add a name and gender. If you already have an account, your phone is enough.";
    public const string DevOtpHint = "Local demo: the Development API uses code 123456 and returns it as devCode. Existing demo phone +91 98765 43210.";
    public const string AccountType = "Account";
    public const string ExistingAccount = "I already have an account";
    public const string NewAccount = "I'm new";
    public const string Name = "Name";
    public const string Gender = "Gender";
    public const string GenderSelect = "Select";
    public const string GenderFemale = "Female";
    public const string GenderMale = "Male";
    public const string GenderOther = "Other";
    public const string Phone = "Phone";
    public const string PhonePlaceholder = "9876543210";
    public const string SendCode = "Send code";
    public const string Code = "Code";
    public const string Verify = "Verify and continue";
    public const string Resend = "Resend code";
    public const string UseDifferentPhone = "Use a different phone";
    public const string CodeSentTo = "We sent a code to {0}.";
    public const string DevCodeLabel = "Development code: {0}";
    public const string CodeExpires = "Expires at {0} IST.";

    public const string PhoneRequired = "Phone is required.";
    public const string NameRequired = "Name is required for a new account.";
    public const string GenderRequired = "Gender is required for a new account. Use Female, Male, or Other.";
    public const string CodeRequired = "Enter the code we sent.";

    public const string AreaTitle = "Choose your area";
    public const string AreaLead = "Mumbai neighbourhoods from the marketplace. You can change this while browsing.";
    public const string ChooseArea = "Choose an area to continue.";
    public const string NoAreas = "No areas are listed yet.";

    public const string BrowseTitle = "Instructors";
    public const string BrowseLead = "Verified instructors only. Filter by area and session mode.";
    public const string VerifiedOnly = "Verified only";
    public const string Area = "Area";
    public const string Mode = "Mode";
    public const string AnyMode = "Any mode";
    public const string ModeHome = "Home";
    public const string ModeStudio = "Studio";
    public const string ModeOnline = "Online";
    public const string ApplyFilters = "Show instructors";
    public const string ViewProfile = "View profile and slots";
    public const string NoInstructors = "No verified instructors in {0} for {1} yet.";
    public const string AnyModePhrase = "any mode";
    public const string RatingLine = "{0} · {1} reviews";

    public const string ProfileTitle = "Instructor";
    public const string BackToList = "Back to instructors";
    public const string StudioAddress = "Studio";
    public const string AgeLabel = "Age {0}";
    public const string SlotsTitle = "Open slots";
    public const string SlotsLead = "Times are Mumbai local time. Booking is not part of this step.";
    public const string NoSlots = "No open slots in this range.";
    public const string SlotRange = "{0} to {1}";
    public const string InstructorNotFound = "That instructor is not available.";

    public const string ApiUnreachable = "We couldn't reach the marketplace. Start the API and try again.";
    public const string EmptyResponse = "The marketplace returned an empty response.";
    public const string GenericError = "Something went wrong. Try again.";
}
