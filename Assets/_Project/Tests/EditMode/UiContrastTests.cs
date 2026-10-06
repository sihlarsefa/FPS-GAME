using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class UiContrastTests
    {
        [Test]
        public void Contrast_BlackWhite_Is21()
        {
            Assert.AreEqual(21f, UiTheme.ContrastRatio(Color.white, Color.black), 0.05f);
        }

        [Test]
        public void Muted_Text_Meets_AA_On_Panels()
        {
            Assert.IsTrue(UiTheme.MeetsTextContrast(UiTheme.TextMuted, UiTheme.PanelLight), "TextMuted/PanelLight");
            Assert.IsTrue(UiTheme.MeetsTextContrast(UiTheme.TextMuted, UiTheme.Panel), "TextMuted/Panel");
            Assert.IsTrue(UiTheme.MeetsTextContrast(UiTheme.TextDim, UiTheme.PanelLight), "TextDim/PanelLight");
            Assert.IsTrue(UiTheme.MeetsTextContrast(UiTheme.Text, UiTheme.ButtonHover), "Text/ButtonHover");
        }

        [Test]
        public void Overlap_Detects()
        {
            Assert.IsTrue(UiQaOverlay.Overlaps(new Rect(0, 0, 10, 10), new Rect(2, 2, 10, 10)));
            Assert.IsFalse(UiQaOverlay.Overlaps(new Rect(0, 0, 10, 10), new Rect(10, 0, 10, 10)));
        }
    }
}
