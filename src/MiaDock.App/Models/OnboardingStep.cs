namespace MiaDock.App.Models;

public enum OnboardingStep
{
    Welcome,
    Startup,
    Appearance,
    Media,
    Display,
    Interaction,
    Fullscreen,
    Modules,
    Summary
}

public sealed record OnboardingStepDefinition(OnboardingStep Step, string Title);
