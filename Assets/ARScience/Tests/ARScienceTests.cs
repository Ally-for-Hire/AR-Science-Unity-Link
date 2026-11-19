using System.Collections.Generic;
using NUnit.Framework;

namespace ARScience.Tests
{
    // Run these in Unity: Window > General > Test Runner > EditMode > Run All.
    // Each test builds a tiny module in code, does one thing, and checks the result.

    /// <summary>Zoom limits and part show/hide rules (test plan: "zoom limits").</summary>
    public class ZoomTests
    {
        [Test]
        public void ZoomStaysInsideModuleLimits()
        {
            var model = new ModelDefinition { minZoom = 0.5f, maxZoom = 4f };
            Assert.AreEqual(4f, ModelRules.ClampZoom(10f, model));
            Assert.AreEqual(0.5f, ModelRules.ClampZoom(0.1f, model));
            Assert.AreEqual(2f, ModelRules.ClampZoom(2f, model));
        }

        [Test]
        public void MissingLimitsUseDefaults()
        {
            var model = new ModelDefinition(); // minZoom / maxZoom left out of the JSON.
            Assert.AreEqual(ModelRules.DefaultMaxZoom, ModelRules.ClampZoom(100f, model));
            Assert.AreEqual(ModelRules.DefaultMinZoom, ModelRules.ClampZoom(0f, model));
        }

        [Test]
        public void InnerPartAppearsWhenZoomedIn()
        {
            var nucleus = new PartDefinition { showFromZoom = 2f };
            Assert.IsFalse(ModelRules.IsVisibleAt(nucleus, 1f));
            Assert.IsTrue(ModelRules.IsVisibleAt(nucleus, 2f));
        }

        [Test]
        public void OuterShellDisappearsWhenZoomedIn()
        {
            var membrane = new PartDefinition { hideFromZoom = 2f };
            Assert.IsTrue(ModelRules.IsVisibleAt(membrane, 1.9f));
            Assert.IsFalse(ModelRules.IsVisibleAt(membrane, 2f));
        }

        [Test]
        public void PartWithNoZoomRulesIsAlwaysVisible()
        {
            var part = new PartDefinition();
            Assert.IsTrue(ModelRules.IsVisibleAt(part, 0.5f));
            Assert.IsTrue(ModelRules.IsVisibleAt(part, 10f));
        }
    }

    /// <summary>Matching JSON part ids to object names (test plan: "part lookup").</summary>
    public class PartLookupTests
    {
        static ModelDefinition Cell() => new ModelDefinition
        {
            item = "celldemo",
            parts = new List<PartDefinition>
            {
                new PartDefinition { id = "Nucleus", name = "Nucleus" },
                new PartDefinition { id = "Cell Membrane", name = "Cell membrane" },
            },
        };

        [Test]
        public void FindsPartByObjectName() =>
            Assert.AreEqual("Nucleus", ModelRules.FindPart(Cell(), "Nucleus").name);

        [Test]
        public void NameMatchIgnoresCaseSpacesUnderscoresAndDashes()
        {
            Assert.IsNotNull(ModelRules.FindPart(Cell(), "nucleus"));
            Assert.IsNotNull(ModelRules.FindPart(Cell(), "cell_membrane"));
            Assert.IsNotNull(ModelRules.FindPart(Cell(), "Cell-Membrane"));
            Assert.IsNotNull(ModelRules.FindPart(Cell(), "CellMembrane"));
        }

        [Test]
        public void ObjectThatIsNotAPartReturnsNull() =>
            Assert.IsNull(ModelRules.FindPart(Cell(), "Ribosome"));
    }

    /// <summary>The checks run on every module file (test plan: "JSON schema validation").</summary>
    public class ModuleValidationTests
    {
        static readonly HashSet<string> Items = new HashSet<string> { "cube", "celldemo" };

        static ModuleDefinition Module(ScenarioDefinition scenario) =>
            new ModuleDefinition { schemaVersion = 1, id = "test", title = "Test", scenario = scenario };

        static ModuleDefinition Explore(params PartDefinition[] parts) => Module(new ScenarioDefinition
        {
            type = "explore",
            models = new List<ModelDefinition> { new ModelDefinition { item = "celldemo", parts = new List<PartDefinition>(parts) } },
        });

        [Test]
        public void GoodPlacementModulePasses()
        {
            var module = Module(new ScenarioDefinition
            {
                type = "placement",
                steps = new List<StepDefinition> { new StepDefinition { item = "cube", count = 2 } },
            });
            CollectionAssert.IsEmpty(ModuleCatalog.Validate(module, Items));
        }

        [Test]
        public void GoodExploreModulePasses() =>
            CollectionAssert.IsEmpty(ModuleCatalog.Validate(Explore(new PartDefinition { id = "Nucleus", name = "Nucleus" }), Items));

        [Test]
        public void UnknownItemIsRejected()
        {
            var module = Explore();
            module.scenario.models[0].item = "dragon";
            Assert.IsNotEmpty(ModuleCatalog.Validate(module, Items));
        }

        [Test]
        public void ExploreWithoutModelsIsRejected() =>
            Assert.IsNotEmpty(ModuleCatalog.Validate(Module(new ScenarioDefinition { type = "explore" }), Items));

        [Test]
        public void UnknownScenarioTypeIsRejected() =>
            Assert.IsNotEmpty(ModuleCatalog.Validate(Module(new ScenarioDefinition { type = "quiz" }), Items));

        [Test]
        public void MinZoomBiggerThanMaxZoomIsRejected()
        {
            var module = Explore();
            module.scenario.models[0].minZoom = 3f;
            module.scenario.models[0].maxZoom = 2f;
            Assert.IsNotEmpty(ModuleCatalog.Validate(module, Items));
        }

        [Test]
        public void ZoomRangeMustIncludePlacedSize()
        {
            var module = Explore();
            module.scenario.models[0].minZoom = 1.5f; // The model would jump bigger the moment it is placed.
            Assert.IsNotEmpty(ModuleCatalog.Validate(module, Items));
        }

        [Test]
        public void PartShownOnlyAboveMaxZoomIsRejected()
        {
            var module = Explore(new PartDefinition { id = "Nucleus", name = "Nucleus", showFromZoom = 5f });
            module.scenario.models[0].maxZoom = 4f;
            Assert.IsNotEmpty(ModuleCatalog.Validate(module, Items));
        }

        [Test]
        public void PartHiddenAtMinZoomIsRejected()
        {
            var module = Explore(new PartDefinition { id = "Membrane", name = "Membrane", hideFromZoom = 0.5f });
            module.scenario.models[0].minZoom = 0.5f;
            Assert.IsNotEmpty(ModuleCatalog.Validate(module, Items));
        }

        [Test]
        public void PartWithoutNameIsRejected() =>
            Assert.IsNotEmpty(ModuleCatalog.Validate(Explore(new PartDefinition { id = "Nucleus" }), Items));

        [Test]
        public void SamePartTwiceIsRejected() =>
            Assert.IsNotEmpty(ModuleCatalog.Validate(Explore(
                new PartDefinition { id = "Nucleus", name = "A" },
                new PartDefinition { id = "nucleus", name = "B" }), Items));

        [Test]
        public void PartThatCanNeverBeSeenIsRejected() =>
            Assert.IsNotEmpty(ModuleCatalog.Validate(Explore(
                new PartDefinition { id = "Nucleus", name = "Nucleus", showFromZoom = 3f, hideFromZoom = 2f }), Items));

        [Test]
        public void WrongSchemaVersionIsRejected()
        {
            var module = Explore();
            module.schemaVersion = 2;
            Assert.IsNotEmpty(ModuleCatalog.Validate(module, Items));
        }
    }

    /// <summary>Step-by-step progress in placement modules.</summary>
    public class ScenarioRunnerTests
    {
        [Test]
        public void StepsRepeatByCountThenFinish()
        {
            var runner = new ScenarioRunner(new ScenarioDefinition
            {
                steps = new List<StepDefinition>
                {
                    new StepDefinition { item = "cube" },             // count left out = 1
                    new StepDefinition { item = "pyramid", count = 2 },
                },
            });
            var order = new List<string>();
            while (!runner.IsComplete) { order.Add(runner.Current.item); runner.Advance(); }
            CollectionAssert.AreEqual(new[] { "cube", "pyramid", "pyramid" }, order);
            Assert.AreEqual(3, runner.Done);
        }

        [Test]
        public void PromptFallsBackToItemName()
        {
            var runner = new ScenarioRunner(new ScenarioDefinition
            {
                steps = new List<StepDefinition> { new StepDefinition { item = "torus" } },
            });
            Assert.AreEqual("Place a torus", runner.CurrentPrompt);
        }
    }

    /// <summary>The real module files in Resources/Modules (test plan: "module loading"). Needs Unity to run.</summary>
    [Category("NeedsUnity")]
    public class ModuleFileTests
    {
        [Test]
        public void EveryModuleFileLoadsAndIsValid()
        {
            var modules = ModuleCatalog.LoadAll();
            Assert.IsNotEmpty(modules, "no files in Resources/Modules");
            foreach (var module in modules)
                CollectionAssert.IsEmpty(ModuleCatalog.Validate(module, null), "module '" + module.id + "'");
        }

        [Test]
        public void BrokenJsonIsRejectedNotCrashing() =>
            Assert.IsNotEmpty(ModuleCatalog.Validate(ModuleCatalog.Parse("{ not json"), null));
    }
}
