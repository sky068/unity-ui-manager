using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Game.UISystem.VContainerIntegration.Tests
{
    public sealed class VContainerUIObjectFactoryTests
    {
        private sealed class Dependency
        {
        }

        private sealed class Receiver : MonoBehaviour
        {
            public Dependency Value { get; private set; }

            [Inject]
            private void Construct(Dependency value)
            {
                Value = value;
            }
        }

        [Test]
        public void Instantiate_InjectsWindowComponents()
        {
            var dependency = new Dependency();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(dependency);
            var resolver = builder.Build();
            var prefab = new GameObject("WindowPrefab");
            var parent = new GameObject("WindowParent");
            prefab.SetActive(false);
            prefab.AddComponent<Receiver>();

            GameObject instance = null;
            try
            {
                var factory = new VContainerUIObjectFactory(resolver);
                instance = factory.Instantiate(prefab, parent.transform);

                Assert.That(instance.GetComponent<Receiver>().Value, Is.SameAs(dependency));
            }
            finally
            {
                resolver.Dispose();
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(parent);
            }
        }
    }
}
