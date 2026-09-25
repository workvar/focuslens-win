namespace FocusLens.Core.Privacy;

/// <summary>
/// Predefined sensitive categories. Strongly sensitive ones default to enabled
/// (excluded); commonly work-related ones (personal email, messaging) default to off.
/// </summary>
public static class PrivacyDefaults
{
    public static PrivacyConfig Create() => new()
    {
        Version = PrivacyConfig.CurrentVersion,
        Categories =
        {
            Make("banking", "Banking & Finance", "banknote", "#2F7F7A", true,
                apps: new string[0],
                domains: new[]
                {
                    "chase.com", "bankofamerica.com", "wellsfargo.com", "citi.com",
                    "capitalone.com", "usbank.com", "pnc.com", "tdbank.com", "ally.com",
                    "americanexpress.com", "discover.com", "hsbc.com", "barclays.co.uk",
                    "paypal.com", "venmo.com", "wise.com", "schwab.com", "fidelity.com",
                    "vanguard.com", "robinhood.com", "sofi.com", "chime.com",
                },
                keywords: new[] { "online banking", "account balance", "routing number", "wire transfer" }),

            Make("password-managers", "Password Managers", "key", "#1A5D38", true,
                apps: new[]
                {
                    "1password.exe", "bitwarden.exe", "lastpass.exe", "enpass.exe",
                    "dashlane.exe", "keepass.exe", "keepassxc.exe", "nordpass.exe",
                },
                domains: new[] { "1password.com", "bitwarden.com", "lastpass.com", "dashlane.com" },
                keywords: new[] { "master password", "seed phrase", "recovery code", "one-time passcode" }),

            Make("health", "Health & Medical", "cross", "#B5603F", true,
                apps: new string[0],
                domains: new[]
                {
                    "mychart.com", "healthcare.gov", "kp.org", "myhealth.va.gov",
                    "webmd.com", "goodrx.com", "zocdoc.com", "teladoc.com",
                    "cvs.com", "walgreens.com",
                },
                keywords: new[] { "diagnosis", "prescription", "medical record", "lab results", "patient portal" }),

            Make("crypto", "Crypto Wallets", "coins", "#8A9A3B", true,
                apps: new[] { "exodus.exe", "ledger live.exe", "trezor suite.exe", "coinbase.exe" },
                domains: new[]
                {
                    "coinbase.com", "binance.com", "kraken.com", "metamask.io",
                    "blockchain.com", "ledger.com", "trezor.io", "crypto.com",
                },
                keywords: new[] { "seed phrase", "private key", "recovery phrase", "wallet address" }),

            Make("adult", "Adult Content", "eye-off", "#C58A2B", true,
                apps: new string[0],
                domains: new[] { "pornhub.com", "onlyfans.com", "xvideos.com", "xhamster.com" },
                keywords: new string[0]),

            Make("personal-email", "Personal Email", "mail", "#64748B", false,
                apps: new string[0],
                domains: new[]
                {
                    "mail.google.com", "outlook.live.com", "mail.yahoo.com",
                    "mail.proton.me", "fastmail.com",
                },
                keywords: new string[0]),

            Make("messaging", "Messaging", "message", "#6B9E7F", false,
                apps: new[] { "whatsapp.exe", "telegram.exe", "signal.exe", "messenger.exe" },
                domains: new[] { "web.whatsapp.com", "web.telegram.org", "messenger.com" },
                keywords: new string[0]),

            Make("others", "Others", "more", "#A8A29E", true,
                apps: new string[0], domains: new string[0], keywords: new string[0]),
        },
    };

    private static PrivacyCategory Make(
        string id, string name, string icon, string color, bool enabled,
        string[] apps, string[] domains, string[] keywords) => new()
    {
        Id = id, Name = name, Icon = icon, ColorHex = color,
        IsSystem = true, IsEnabled = enabled,
        AppIds = apps.ToList(), Domains = domains.ToList(), Keywords = keywords.ToList(),
    };
}
