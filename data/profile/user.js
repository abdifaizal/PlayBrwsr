// PlayBrwsr Portable Preferences
user_pref("toolkit.legacyUserProfileCustomizations.stylesheets", true);
user_pref("browser.tabs.inTitlebar", 1);
user_pref("browser.tabs.drawInTitlebar", true);
user_pref("ui.systemUsesDarkTheme", 1);
user_pref("browser.theme.content-theme", 0);
user_pref("browser.theme.toolbar-theme", 0);

// Skip first-run, welcome, and default browser prompts
user_pref("browser.shell.checkDefaultBrowser", false);
user_pref("browser.startup.homepage_override.mstone", "ignore");
user_pref("trailhead.firstrun.didSeeAboutWelcome", true);
user_pref("browser.aboutwelcome.enabled", false);
user_pref("browser.startup.firstrunSkipsHomepage", false);
user_pref("startup.homepage_welcome_url", "");
user_pref("startup.homepage_welcome_url.additional", "");
user_pref("browser.messaging-system.whatsNewPanel.enabled", false);

// Startup pages: YouTube Video & YouTube Music
user_pref("browser.startup.page", 1);
user_pref("browser.startup.homepage", "https://www.youtube.com|https://music.youtube.com");

// Auto-enable extensions (profile scope only, ignore host machine extensions)
user_pref("extensions.autoDisableScopes", 0);
user_pref("extensions.enabledScopes", 1);
user_pref("extensions.update.autoUpdateDefault", false);
user_pref("extensions.webextensions.uuids", "{\"browsec@browsec.com\":\"bba20292-a654-403f-a1f8-90d0db093b15\"}");

// Disable Firefox telemetry, Pocket, Sync, and clutter
user_pref("datareporting.healthreport.uploadEnabled", false);
user_pref("datareporting.policy.dataSubmissionEnabled", false);
user_pref("toolkit.telemetry.enabled", false);
user_pref("toolkit.telemetry.unified", false);
user_pref("extensions.pocket.enabled", false);
user_pref("identity.fxaccounts.enabled", false);
user_pref("browser.newtabpage.activity-stream.feeds.telemetry", false);
user_pref("browser.newtabpage.activity-stream.telemetry", false);
user_pref("browser.newtabpage.activity-stream.showSponsored", false);
user_pref("browser.newtabpage.activity-stream.showSponsoredTopSites", false);

// Media playback & performance
user_pref("media.autoplay.default", 0);
user_pref("media.hardware-video-decoding.enabled", true);
user_pref("browser.tabs.warnOnClose", false);
user_pref("browser.sessionstore.resume_from_crash", false);
user_pref("breakpad.reportURL", "");
user_pref("browser.crashReports.unsubmittedCheck.enabled", false);
user_pref("browser.crashReports.unsubmittedCheck.autoSubmit2", false);
