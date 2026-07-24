using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.UISystem.Tests
{
    public sealed class UIManagerPlayModeTests
    {
        [UnityTest]
        public IEnumerator SingleWindow_ReusesExistingHandle()
        {
            SceneManager.LoadScene("UIWindowTestCases", LoadSceneMode.Single);
            yield return null;

            var ui = UIManager.Instance;
            Assert.That(ui, Is.Not.Null);
            ui.CloseAllImmediately();

            var first = ui.Open(UIWindowId.SettingWindow);
            var second = ui.Open(UIWindowId.SettingWindow);

            Assert.That(second, Is.SameAs(first));
            Assert.That(ui.OpenCount, Is.EqualTo(1));

            first.Close();
            yield return new UnityEngine.WaitUntil(
                () => first.Closed.Status != UniTaskStatus.Pending);
            Assert.That(first.Closed.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(ui.OpenCount, Is.Zero);
        }

        [Test]
        public void PlainText_IsBoundedWithoutSplittingSurrogatePair()
        {
            string value = "ab\U0001F600cd";
            Assert.That(UITextSafety.NormalizePlainText(value, 3), Is.EqualTo("ab"));
            Assert.That(UITextSafety.NormalizePlainText(value, 4), Is.EqualTo("ab\U0001F600"));
        }

        [UnityTest]
        public IEnumerator SceneButtons_HaveValidPersistentCallbacks()
        {
            SceneManager.LoadScene("UIWindowTestCases", LoadSceneMode.Single);
            yield return null;

            var buttons = Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(buttons, Is.Not.Empty);
            foreach (var button in buttons)
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    Assert.That(button.onClick.GetPersistentTarget(i), Is.Not.Null,
                        $"{button.name} 的持久化点击目标已失效");
                    Assert.That(button.onClick.GetPersistentMethodName(i), Is.Not.Empty,
                        $"{button.name} 的持久化点击方法已失效");
                }
            }
        }

        [TestCase("UISystem/Icons/reward", "UISystem/Icons/reward")]
        [TestCase("../secret", null)]
        [TestCase("Other/reward", null)]
        public void ToastIconPath_AllowsOnlyDedicatedDirectory(string input, string expected)
        {
            Assert.That(UITextSafety.NormalizeToastIconPath(input), Is.EqualTo(expected));
        }
    }
}
