using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class OnboardingWindowTests
{
    [TestMethod]
    public void Window_ContainsStepListAndAccessibleNavigationButtons()
    {
        var document = Load("Windows", "OnboardingWindow.xaml");
        var text = document.ToString();

        StringAssert.Contains(text, "ItemsSource=\"{Binding Steps}\"");
        StringAssert.Contains(text, "OnBackClick");
        StringAssert.Contains(text, "OnNextClick");
        StringAssert.Contains(text, "Önceki kurulum adımı");
        StringAssert.Contains(text, "Sonraki kurulum adımı");
    }

    [TestMethod]
    public void Wizard_DefinesAllNineRequiredSteps()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Onboarding"), "*StepView.xaml");

        Assert.AreEqual(9, files.Length);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "WelcomeStepView.xaml", "StartupStepView.xaml", "AppearanceStepView.xaml", "MediaStepView.xaml",
                "DisplayStepView.xaml", "InteractionStepView.xaml", "FullscreenStepView.xaml",
                "ModulesStepView.xaml", "SummaryStepView.xaml"
            },
            files.Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void ModulesStep_ExplainsThatSelectedServicesRequireConsent()
    {
        var document = Load("Onboarding", "ModulesStepView.xaml");
        var text = document.ToString();

        StringAssert.Contains(text, "kullanılan servisler açıklanır");
        StringAssert.Contains(text, "onayınız istenir");
        StringAssert.Contains(text, "CanSelectDuringOnboarding");
    }

    [TestMethod]
    public void PrivacyStep_StatesOfflineAndNoTelemetryBehavior()
    {
        var document = Load("Onboarding", "WelcomeStepView.xaml");
        var text = document.ToString();

        StringAssert.Contains(text, "çevrimdışı");
        StringAssert.Contains(text, "telemetri");
    }

    private static XDocument Load(params string[] segments) =>
        XDocument.Load(Path.Combine([AppContext.BaseDirectory, .. segments]));
}
