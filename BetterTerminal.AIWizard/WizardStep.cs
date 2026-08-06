using System.Collections.Generic;

namespace BetterTerminal.AIWizard
{
    /// <summary>
    /// One option in a menu step. When <see cref="PromptValue"/> is false the option contributes
    /// <see cref="Fragment"/> to the command line as written. When it is true, the caller asks for a
    /// value and substitutes it into <see cref="Fragment"/> at the "{0}" placeholder - and an empty
    /// answer contributes nothing at all, so a prompted option that is left blank is the same as a
    /// skip.
    /// </summary>
    public sealed class WizardChoice
    {
        public WizardChoice(char key, string label, string fragment)
        {
            Key = char.ToUpperInvariant(key);
            Label = label;
            Fragment = fragment ?? string.Empty;
        }

        public char Key { get; private set; }

        public string Label { get; private set; }

        /// <summary>A short hint shown after the label, or null.</summary>
        public string Hint { get; set; }

        /// <summary>
        /// The text this option adds to the command line. When <see cref="PromptValue"/> is set it
        /// holds a single "{0}" where the entered value goes.
        /// </summary>
        public string Fragment { get; private set; }

        public bool PromptValue { get; set; }

        /// <summary>The question shown when <see cref="PromptValue"/> is set.</summary>
        public string ValuePrompt { get; set; }

        /// <summary>Fluent helper: turn this into an option that asks for a value first.</summary>
        public WizardChoice AskingFor(string prompt)
        {
            PromptValue = true;
            ValuePrompt = prompt;
            return this;
        }

        public WizardChoice WithHint(string hint)
        {
            Hint = hint;
            return this;
        }
    }

    /// <summary>
    /// One screen of the wizard. Ordinary steps are a menu of <see cref="WizardChoice"/>. A model
    /// step is drawn from the model file at run time instead, because its options are not known when
    /// the flow is defined; it contributes <see cref="ModelFlag"/> followed by the chosen id.
    /// </summary>
    public sealed class WizardStep
    {
        public string Title { get; set; }

        /// <summary>A line of guidance under the title, or null.</summary>
        public string Note { get; set; }

        public bool IsModel { get; set; }

        /// <summary>The flag a chosen model is written behind, e.g. "--model" or "-m".</summary>
        public string ModelFlag { get; set; }

        /// <summary>Which engine's model list to offer, for a model step.</summary>
        public AiEngine Engine { get; set; }

        public IList<WizardChoice> Choices { get; set; }

        public static WizardStep Menu(string title, params WizardChoice[] choices)
        {
            return new WizardStep { Title = title, Choices = new List<WizardChoice>(choices) };
        }

        public static WizardStep Model(string title, AiEngine engine, string modelFlag)
        {
            return new WizardStep { Title = title, IsModel = true, Engine = engine, ModelFlag = modelFlag };
        }
    }
}
