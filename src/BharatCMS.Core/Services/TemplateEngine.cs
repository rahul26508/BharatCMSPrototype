namespace BharatCMS.Core.Services;

/// <summary>
/// Master Template Engine: Dynamically renders layouts, navigation, and forms
/// based on tenant type metadata configuration (School, University, Anganwadi, Mining, District).
/// Zero hardcoded HTML - everything parsed from JSON metadata at runtime.
/// </summary>
public class TemplateEngine
{
    private readonly Dictionary<TenantTemplateType, TemplateConfig> _templates;

    public TemplateEngine()
    {
        _templates = InitializeDefaultTemplates();
    }

    /// <summary>
    /// Get template configuration for a tenant type.
    /// </summary>
    public TemplateConfig GetTemplate(TenantTemplateType type)
    {
        return _templates.GetValueOrDefault(type) ?? _templates[TenantTemplateType.School];
    }

    /// <summary>
    /// Parse custom metadata JSON and merge with base template.
    /// </summary>
    public TemplateConfig ParseMetadata(string? metadataJson, TenantTemplateType baseType)
    {
        var baseTemplate = GetTemplate(baseType);

        if (string.IsNullOrEmpty(metadataJson))
            return baseTemplate;

        try
        {
            var custom = System.Text.Json.JsonSerializer.Deserialize<TemplateConfig>(metadataJson);
            return MergeTemplates(baseTemplate, custom!);
        }
        catch
        {
            return baseTemplate;
        }
    }

    private TemplateConfig MergeTemplates(TemplateConfig baseConfig, TemplateConfig overrideConfig)
    {
        return new TemplateConfig
        {
            Type = baseConfig.Type,
            BrandName = overrideConfig.BrandName ?? baseConfig.BrandName,
            PrimaryColor = overrideConfig.PrimaryColor ?? baseConfig.PrimaryColor,
            SecondaryColor = overrideConfig.SecondaryColor ?? baseConfig.SecondaryColor,
            Logo = overrideConfig.Logo ?? baseConfig.Logo,
            HeroText = overrideConfig.HeroText ?? baseConfig.HeroText,
            Navigation = overrideConfig.Navigation ?? baseConfig.Navigation,
            Modules = overrideConfig.Modules ?? baseConfig.Modules,
            Forms = overrideConfig.Forms ?? baseConfig.Forms,
            Footer = overrideConfig.Footer ?? baseConfig.Footer,
            SupportedLanguages = overrideConfig.SupportedLanguages ?? baseConfig.SupportedLanguages
        };
    }

    private Dictionary<TenantTemplateType, TemplateConfig> InitializeDefaultTemplates()
    {
        return new Dictionary<TenantTemplateType, TemplateConfig>
        {
            [TenantTemplateType.School] = new TemplateConfig
            {
                Type = TenantTemplateType.School,
                BrandName = "Government School",
                PrimaryColor = "#1565C0",
                SecondaryColor = "#0D47A1",
                HeroText = "Empowering Education for a Better Tomorrow",
                Navigation = new NavigationConfig
                {
                    Items = new List<NavItem>
                    {
                        new() { Key = "home", Label = "Home", Icon = "school", Path = "/" },
                        new() { Key = "notices", Label = "Notices", Icon = "announcement", Path = "/notices" },
                        new() { Key = "staff", Label = "Staff", Icon = "people", Path = "/staff" },
                        new() { Key = "results", Label = "Results", Icon = "assessment", Path = "/results" },
                        new() { Key = "gallery", Label = "Gallery", Icon = "photo", Path = "/gallery" },
                        new() { Key = "contact", Label = "Contact", Icon = "phone", Path = "/contact" }
                    },
                    ShowQuickLinks = true,
                    ShowEmergencyContact = true
                },
                Modules = new List<string> { "notices", "staff", "results", "gallery", "chatbot", "documents" },
                Forms = new List<FormConfig>
                {
                    new() { Type = "enquiry", Title = "Student Enquiry", Fields = new[] { "student_name", "parent_name", "class", "phone", "message" } },
                    new() { Type = "admission", Title = "Admission Form", Fields = new[] { "student_name", "dob", "father_name", "mother_name", "address", "phone", "aadhaar", "previous_school" } }
                },
                Footer = new FooterConfig
                {
                    ShowUsefulLinks = true,
                    ShowQuickConnect = true,
                    ShowGovtLinks = true
                },
                SupportedLanguages = new[] { "en", "hi" }
            },

            [TenantTemplateType.University] = new TemplateConfig
            {
                Type = TenantTemplateType.University,
                BrandName = "Central University",
                PrimaryColor = "#4A148C",
                SecondaryColor = "#311B92",
                HeroText = "Excellence in Higher Education",
                Navigation = new NavigationConfig
                {
                    Items = new List<NavItem>
                    {
                        new() { Key = "home", Label = "Home", Icon = "account_balance", Path = "/" },
                        new() { Key = "departments", Label = "Departments", Icon = "category", Path = "/departments" },
                        new() { Key = "admissions", Label = "Admissions", Icon = "school", Path = "/admissions" },
                        new() { Key = "research", Label = "Research", Icon = "science", Path = "/research" },
                        new() { Key = "notices", Label = "Notices", Icon = "announcement", Path = "/notices" },
                        new() { Key = "placement", Label = "Placements", Icon = "work", Path = "/placement" },
                        new() { Key = "contact", Label = "Contact", Icon = "phone", Path = "/contact" }
                    },
                    ShowQuickLinks = true,
                    ShowAnnouncements = true
                },
                Modules = new List<string> { "notices", "departments", "admissions", "research", "placement", "chatbot", "documents", "gallery" },
                Forms = new List<FormConfig>
                {
                    new() { Type = "application", Title = "Course Application", Fields = new[] { "name", "qualification", "course", "email", "phone", "address", "aadhaar" } },
                    new() { Type = "noc", Title = "NOC Request", Fields = new[] { "enrollment", "name", "purpose", "department" } }
                },
                Footer = new FooterConfig
                {
                    ShowUsefulLinks = true,
                    ShowQuickConnect = true,
                    ShowGovtLinks = true,
                    ShowAccreditionBadges = true
                },
                SupportedLanguages = new[] { "en", "hi" }
            },

            [TenantTemplateType.Anganwadi] = new TemplateConfig
            {
                Type = TenantTemplateType.Anganwadi,
                BrandName = "Anganwadi Centre",
                PrimaryColor = "#E65100",
                SecondaryColor = "#BF360C",
                HeroText = "Nurturing Young Minds & Mothers",
                Navigation = new NavigationConfig
                {
                    Items = new List<NavItem>
                    {
                        new() { Key = "home", Label = "Home", Icon = "home", Path = "/" },
                        new() { Key = "services", Label = "Services", Icon = "child_care", Path = "/services" },
                        new() { Key = "nutrition", Label = "Nutrition", Icon = "restaurant", Path = "/nutrition" },
                        new() { Key = "immunization", Label = "Immunization", Icon = "vaccines", Path = "/immunization" },
                        new() { Key = "notices", Label = "Notices", Icon = "announcement", Path = "/notices" },
                        new() { Key = "contact", Label = "Contact", Icon = "phone", Path = "/contact" }
                    },
                    ShowEmergencyContact = true,
                    ShowHealthAlerts = true
                },
                Modules = new List<string> { "notices", "services", "chatbot", "documents" },
                Forms = new List<FormConfig>
                {
                    new() { Type = "registration", Title = "Child Registration", Fields = new[] { "child_name", "dob", "mother_name", "father_name", "aadhaar_child", "aadhaar_mother", "address" } },
                    new() { Type = "nutrition", Title = "Nutrition Survey", Fields = new[] { "name", "age", "weight", "height", "muac" } }
                },
                Footer = new FooterConfig
                {
                    ShowUsefulLinks = true,
                    ShowQuickConnect = true,
                    ShowGovtLinks = true
                },
                SupportedLanguages = new[] { "en", "hi", "mr" }
            },

            [TenantTemplateType.Mining] = new TemplateConfig
            {
                Type = TenantTemplateType.Mining,
                BrandName = "Mining Department",
                PrimaryColor = "#5D4037",
                SecondaryColor = "#3E2723",
                HeroText = "Sustainable Mining for National Development",
                Navigation = new NavigationConfig
                {
                    Items = new List<NavItem>
                    {
                        new() { Key = "home", Label = "Home", Icon = "domain", Path = "/" },
                        new() { Key = "leases", Label = "Mining Leases", Icon = "description", Path = "/leases" },
                        new() { Key = "applications", Label = "Applications", Icon = "assignment", Path = "/applications" },
                        new() { Key = "inspections", Label = "Inspections", Icon = "fact_check", Path = "/inspections" },
                        new() { Key = "notices", Label = "Notices", Icon = "announcement", Path = "/notices" },
                        new() { Key = "compliance", Label = "Compliance", Icon = "verified", Path = "/compliance" },
                        new() { Key = "contact", Label = "Contact", Icon = "phone", Path = "/contact" }
                    },
                    ShowQuickLinks = true,
                    ShowStatutoryAlerts = true
                },
                Modules = new List<string> { "notices", "leases", "applications", "inspections", "compliance", "chatbot", "documents" },
                Forms = new List<FormConfig>
                {
                    new() { Type = "lease_application", Title = "Mining Lease Application", Fields = new[] { "company_name", "mineral_type", "area_hectares", "district", "tehsil", "village", "period", "contact_person", "email", "phone" } },
                    new() { Type = "inspection_report", Title = "Inspection Report", Fields = new[] { "mine_name", "inspection_date", "inspector_name", "findings", "compliance_status" } }
                },
                Footer = new FooterConfig
                {
                    ShowUsefulLinks = true,
                    ShowQuickConnect = true,
                    ShowGovtLinks = true
                },
                SupportedLanguages = new[] { "en", "hi" }
            },

            [TenantTemplateType.District] = new TemplateConfig
            {
                Type = TenantTemplateType.District,
                BrandName = "District Administration",
                PrimaryColor = "#B71C1C",
                SecondaryColor = "#7F0000",
                HeroText = "Serving Citizens, Building Communities",
                Navigation = new NavigationConfig
                {
                    Items = new List<NavItem>
                    {
                        new() { Key = "home", Label = "Home", Icon = "home", Path = "/" },
                        new() { Key = "rti", Label = "RTI", Icon = "info", Path = "/rti" },
                        new() { Key = "schemes", Label = "Schemes", Icon = "volunteer_activism", Path = "/schemes" },
                        new() { Key = "grievance", Label = "Grievance", Icon = "feedback", Path = "/grievance" },
                        new() { Key = "notices", Label = "Notices", Icon = "announcement", Path = "/notices" },
                        new() { Key = "officers", Label = "Officers", Icon = "people", Path = "/officers" },
                        new() { Key = "emergency", Label = "Emergency", Icon = "emergency", Path = "/emergency" },
                        new() { Key = "contact", Label = "Contact", Icon = "phone", Path = "/contact" }
                    },
                    ShowQuickLinks = true,
                    ShowEmergencyNumbers = true,
                    ShowCitizenCharter = true
                },
                Modules = new List<string> { "notices", "rti", "schemes", "grievance", "officers", "emergency", "chatbot", "documents", "gallery" },
                Forms = new List<FormConfig>
                {
                    new() { Type = "rti_application", Title = "RTI Application", Fields = new[] { "name", "address", "phone", "subject", "details", "preferred_mode" } },
                    new() { Type = "grievance", Title = "Citizen Grievance", Fields = new[] { "name", "phone", "email", "address", "department", "subject", "description" } }
                },
                Footer = new FooterConfig
                {
                    ShowUsefulLinks = true,
                    ShowQuickConnect = true,
                    ShowGovtLinks = true,
                    ShowCitizenCharter = true,
                    ShowSocialMedia = true
                },
                SupportedLanguages = new[] { "en", "hi", "mr", "bn", "te", "ta" }
            }
        };
    }
}

public class TemplateConfig
{
    public TenantTemplateType Type { get; set; }
    public string BrandName { get; set; } = "";
    public string PrimaryColor { get; set; } = "#1565C0";
    public string SecondaryColor { get; set; } = "#0D47A1";
    public string? Logo { get; set; }
    public string HeroText { get; set; } = "";
    public NavigationConfig? Navigation { get; set; }
    public List<string>? Modules { get; set; }
    public List<FormConfig>? Forms { get; set; }
    public FooterConfig? Footer { get; set; }
    public string[]? SupportedLanguages { get; set; }
}

public class NavigationConfig
{
    public List<NavItem> Items { get; set; } = new();
    public bool ShowQuickLinks { get; set; }
    public bool ShowEmergencyContact { get; set; }
    public bool ShowAnnouncements { get; set; }
    public bool ShowStatutoryAlerts { get; set; }
    public bool ShowHealthAlerts { get; set; }
    public bool ShowEmergencyNumbers { get; set; }
    public bool ShowCitizenCharter { get; set; }
}

public class NavItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Path { get; set; } = "";
    public bool RequiresAuth { get; set; }
    public string[]? Roles { get; set; }
}

public class FormConfig
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string[] Fields { get; set; } = Array.Empty<string>();
}

public class FooterConfig
{
    public bool ShowUsefulLinks { get; set; }
    public bool ShowQuickConnect { get; set; }
    public bool ShowGovtLinks { get; set; }
    public bool ShowAccreditionBadges { get; set; }
    public bool ShowCitizenCharter { get; set; }
    public bool ShowSocialMedia { get; set; }
}

public enum TenantTemplateType
{
    School = 1,
    University = 2,
    Anganwadi = 3,
    Mining = 4,
    District = 5
}

/// <summary>
/// Maps TenantType to TemplateType for template resolution.
/// </summary>
public static class TenantTypeMapper
{
    public static TenantTemplateType MapToTemplateType(Domain.Entities.TenantType tenantType)
    {
        return tenantType switch
        {
            Domain.Entities.TenantType.School => TenantTemplateType.School,
            Domain.Entities.TenantType.University => TenantTemplateType.University,
            Domain.Entities.TenantType.Anganwadi => TenantTemplateType.Anganwadi,
            Domain.Entities.TenantType.Mining => TenantTemplateType.Mining,
            Domain.Entities.TenantType.District => TenantTemplateType.District,
            _ => TenantTemplateType.School
        };
    }
}