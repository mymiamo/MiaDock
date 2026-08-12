namespace MiaDock.App.Models;

public enum OnboardingStep
{
    Welcome,
    Personalization,
    Interaction,
    FeaturesAndPrivacy,
    Ready
}

public sealed record OnboardingStepDefinition(OnboardingStep Step, string Title);
