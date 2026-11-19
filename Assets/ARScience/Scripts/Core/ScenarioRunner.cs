using System.Collections.Generic;

namespace ARScience
{
    /// <summary>
    /// Tracks progress through a scenario. It knows nothing about Unity, AR or UI:
    /// it just answers "what should the user do now?" and moves on when told.
    /// That keeps it simple to test and reusable for other scenario types.
    /// </summary>
    public class ScenarioRunner
    {
        // Every individual action, in order. A step with count 2 appears twice.
        readonly List<StepDefinition> m_Actions = new List<StepDefinition>();
        int m_Done;

        public ScenarioRunner(ScenarioDefinition scenario)
        {
            foreach (var step in scenario.steps)
            {
                int count = step.count == 0 ? 1 : step.count;
                for (int i = 0; i < count; i++) m_Actions.Add(step);
            }
        }

        /// <summary>Number of actions in the whole scenario.</summary>
        public int Total => m_Actions.Count;

        /// <summary>Number of actions already completed.</summary>
        public int Done => m_Done;

        public bool IsComplete => m_Done >= m_Actions.Count;

        /// <summary>The action the user should do next, or null when finished.</summary>
        public StepDefinition Current => IsComplete ? null : m_Actions[m_Done];

        /// <summary>Text to show for the current action.</summary>
        public string CurrentPrompt
        {
            get
            {
                if (Current == null) return "";
                return string.IsNullOrEmpty(Current.prompt) ? "Place a " + Current.item : Current.prompt;
            }
        }

        /// <summary>Size multiplier for the current action (1 if unset).</summary>
        public float CurrentScale => (Current == null || Current.scale == 0) ? 1f : Current.scale;

        /// <summary>Marks the current action done.</summary>
        public void Advance() { if (!IsComplete) m_Done++; }

        /// <summary>Starts over from the first action.</summary>
        public void Reset() { m_Done = 0; }
    }
}
