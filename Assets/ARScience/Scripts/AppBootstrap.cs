using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Templates.AR;

namespace ARScience
{
    /// <summary>
    /// The app entry point. It creates itself when any scene loads, builds every
    /// screen from code, and shows one screen at a time.
    ///
    /// To add a screen: write a Build___ method that returns its panel, register it
    /// in BuildUi with a name, and call Show("name") from a button.
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        // Screen names. Used as keys so screens can be shown by name.
        const string Home = "Home", Modules = "Modules", Settings = "Settings", Join = "Join", Scenario = "Scenario";

        UiBuilder m_Ui;
        ARScenarioController m_Controller;
        Transform m_Root;
        readonly Dictionary<string, RectTransform> m_Screens = new Dictionary<string, RectTransform>();
        string m_CurrentScreen;

        // Scenario-screen widgets that change while playing.
        Text m_Instructions, m_Prompt, m_ProgressText;
        GameObject m_CompletePanel;

        /// <summary>Runs automatically after every scene load; makes sure exactly one app object exists.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindAnyObjectByType<AppBootstrap>() == null)
                new GameObject("ARScienceApp").AddComponent<AppBootstrap>();
        }

        void Awake()
        {
            DisableTemplateUi();
            EnsureEventSystem();

            m_Controller = gameObject.AddComponent<ARScenarioController>();
            m_Controller.Progress += RefreshScenarioScreen;

            m_Ui = new UiBuilder();
            BuildUi();
            Show(Home);
        }

        void Update()
        {
            // Escape also covers the Android back button.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) GoBack();
        }

        // ------------------------------------------------------------ setup

        /// <summary>The AR template ships its own buttons and onboarding; switch them off so only ours show.</summary>
        static void DisableTemplateUi()
        {
            var goals = FindAnyObjectByType<GoalManager>(FindObjectsInactive.Include);
            if (goals != null) goals.gameObject.SetActive(false);
            var menu = FindAnyObjectByType<ARTemplateMenuManager>(FindObjectsInactive.Include);
            if (menu != null) menu.enabled = false;
        }

        /// <summary>UI needs an EventSystem to receive taps. Reuse the scene's, or make one.</summary>
        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        void BuildUi()
        {
            m_Root = m_Ui.CreateSafeAreaRoot(transform);
            m_Screens[Home] = BuildHome();
            m_Screens[Modules] = BuildModules();
            m_Screens[Settings] = BuildSettings();
            m_Screens[Join] = BuildJoin();
            m_Screens[Scenario] = BuildScenarioScreen();
        }

        // ------------------------------------------------------------ navigation

        /// <summary>Shows one screen and hides the others.</summary>
        void Show(string screen)
        {
            m_CurrentScreen = screen;
            foreach (var pair in m_Screens) pair.Value.gameObject.SetActive(pair.Key == screen);
        }

        /// <summary>Back key behaviour: leave a scenario, or return to Home from a sub-screen.</summary>
        void GoBack()
        {
            if (m_CurrentScreen == Scenario) ExitScenario();
            else if (m_CurrentScreen != Home) Show(Home);
        }

        /// <summary>Adds a "Back" button that returns to Home.</summary>
        void AddBackButton(Transform screen)
        {
            m_Ui.Button(screen, "Back", new Vector2(0.5f, 0.07f), () => Show(Home), 500, 130);
        }

        // ------------------------------------------------------------ screens

        RectTransform BuildHome()
        {
            var screen = m_Ui.Panel(Home, m_Root, true);
            m_Ui.Label(screen, "AR Science", 80, new Vector2(0.5f, 0.75f), new Vector2(900, 140));
            m_Ui.Button(screen, "Modules", new Vector2(0.5f, 0.55f), () => Show(Modules));
            m_Ui.Button(screen, "Settings", new Vector2(0.5f, 0.42f), () => Show(Settings));
            m_Ui.Button(screen, "Join class", new Vector2(0.5f, 0.29f), () => Show(Join));
            return screen;
        }

        /// <summary>One card per module file; tapping a card starts it.</summary>
        RectTransform BuildModules()
        {
            var screen = m_Ui.Panel(Modules, m_Root, true);
            m_Ui.Label(screen, "Modules", 64, new Vector2(0.5f, 0.88f), new Vector2(900, 120));
            AddBackButton(screen);

            var modules = ModuleCatalog.LoadAll();
            if (modules.Count == 0)
                m_Ui.Label(screen, "No modules found", 40, new Vector2(0.5f, 0.6f), new Vector2(900, 100));

            float y = 0.7f; // Cards stack downward from here.
            foreach (var module in modules)
            {
                var captured = module; // Needed so each button remembers its own module.
                var errors = ModuleCatalog.Validate(module, m_Controller.KnownItems);
                string detail = errors.Count > 0 ? "Unavailable: " + errors[0] : module.description;

                var card = m_Ui.Button(screen, module.title + "\n<size=32>" + detail + "</size>",
                    new Vector2(0.5f, y), () => StartScenario(captured), 920, 220);
                if (errors.Count > 0) UiBuilder.Disable(card);
                y -= 0.14f;
            }
            return screen;
        }

        RectTransform BuildSettings()
        {
            var screen = m_Ui.Panel(Settings, m_Root, true);
            m_Ui.Label(screen, "Settings", 64, new Vector2(0.5f, 0.88f), new Vector2(900, 120));
            AddBackButton(screen);

            // A button that flips a saved on/off setting and shows its state.
            var toggle = m_Ui.Button(screen, "", new Vector2(0.5f, 0.7f), null, 920, 140);
            var toggleText = toggle.GetComponentInChildren<Text>();
            void Refresh() => toggleText.text = "Show plane visualization: " + (AppSettings.ShowPlanes ? "ON" : "OFF");
            toggle.onClick.AddListener(() => { AppSettings.ShowPlanes = !AppSettings.ShowPlanes; Refresh(); });
            Refresh();
            return screen;
        }

        /// <summary>Placeholder: the Join button exists but is intentionally disabled.</summary>
        RectTransform BuildJoin()
        {
            var screen = m_Ui.Panel(Join, m_Root, true);
            m_Ui.Label(screen, "Join class", 64, new Vector2(0.5f, 0.88f), new Vector2(900, 120));
            AddBackButton(screen);
            m_Ui.Label(screen, "Joining a class isn't available yet.\nThis feature is not functional.",
                40, new Vector2(0.5f, 0.62f), new Vector2(900, 200));
            UiBuilder.Disable(m_Ui.Button(screen, "Join", new Vector2(0.5f, 0.45f), null));
            return screen;
        }

        /// <summary>Heads-up display shown over the AR camera while a scenario runs.</summary>
        RectTransform BuildScenarioScreen()
        {
            // Transparent panel: taps pass through to the AR view for placing objects.
            var screen = m_Ui.Panel(Scenario, m_Root, false);
            m_Instructions = m_Ui.Label(screen, "", 34, new Vector2(0.5f, 0.94f), new Vector2(980, 140));
            m_Prompt = m_Ui.Label(screen, "", 48, new Vector2(0.5f, 0.86f), new Vector2(980, 100));
            m_ProgressText = m_Ui.Label(screen, "", 40, new Vector2(0.5f, 0.80f), new Vector2(980, 80));
            m_Ui.Button(screen, "Exit", new Vector2(0.5f, 0.07f), ExitScenario, 500, 130);

            // "Complete" overlay, shown only when every step is done.
            var complete = m_Ui.Panel("Complete", screen, false);
            m_Ui.Label(complete, "Scenario complete", 56, new Vector2(0.5f, 0.55f), new Vector2(900, 120));
            m_Ui.Button(complete, "Restart", new Vector2(0.5f, 0.45f), () => m_Controller.Restart(), 500, 130);
            m_CompletePanel = complete.gameObject;
            m_CompletePanel.SetActive(false);
            return screen;
        }

        // ------------------------------------------------------------ scenario flow

        void StartScenario(ModuleDefinition module)
        {
            m_Instructions.text = module.scenario.instructions;
            Show(Scenario);
            m_Controller.Begin(module);
        }

        void ExitScenario()
        {
            m_Controller.End();
            Show(Modules);
        }

        /// <summary>Redraws the heads-up display from the runner's current state.</summary>
        void RefreshScenarioScreen()
        {
            var runner = m_Controller.Runner;
            if (runner == null) return;
            m_CompletePanel.SetActive(runner.IsComplete);
            m_Prompt.text = runner.CurrentPrompt;
            m_ProgressText.text = runner.Done + "/" + runner.Total;
        }
    }
}
