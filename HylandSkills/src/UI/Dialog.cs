using System;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using static HylandSkills.UI.UiKit;

namespace HylandSkills.UI
{
    /// <summary>Modal confirmation card inside the app.</summary>
    public class Dialog
    {
        readonly RectTransform root;
        readonly TextMeshProUGUI title, body, cancelText, okText;
        readonly Image cancel, ok;
        Action onOk;

        public Dialog(RectTransform parent)
        {
            root = Rect("Dialog", parent).Stretch();
            var dim = Image("Dim", root, new Color(0, 0, 0, 0.6f), null, true);
            dim.rectTransform.Stretch();

            var card = Image("Card", root, Theme.Surface, Round16);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Metrics.Node * 8.6f, Metrics.Node * 4.1f));
            var c = card.transform;
            title = Label("Title", c, "", Metrics.FontHeader, Theme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad * 1.4f, -Metrics.Pad * 1.4f - Metrics.FontHeader * 1.3f), new Vector2(-Metrics.Pad * 1.4f, -Metrics.Pad * 1.4f));
            body = Label("Body", c, "", Metrics.FontBody, Theme.TextDim, TextAlignmentOptions.TopLeft);
            body.rectTransform.Stretch(Metrics.Pad * 1.4f, Metrics.Node * 1.1f, Metrics.Pad * 1.4f, Metrics.Pad * 1.6f + Metrics.FontHeader * 1.4f);

            cancel = Image("Cancel", c, Theme.SurfaceHi, Round8);
            cancel.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(Metrics.Pad * 1.4f, Metrics.Pad * 1.4f), new Vector2(-Metrics.Pad * 0.4f, Metrics.Pad * 1.4f + Metrics.Node * 0.62f));
            Button(cancel, Hide, Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
            cancelText = Label("Text", cancel.transform, "Cancel", Metrics.FontBody, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            cancelText.rectTransform.Stretch();

            ok = Image("Ok", c, Theme.Warn, Round8);
            ok.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(Metrics.Pad * 0.4f, Metrics.Pad * 1.4f), new Vector2(-Metrics.Pad * 1.4f, Metrics.Pad * 1.4f + Metrics.Node * 0.62f));
            Button(ok, Confirm, Theme.Warn, Lighten(Theme.Warn, 0.1f), Darken(Theme.Warn, 0.2f));
            okText = Label("Text", ok.transform, "OK", Metrics.FontBody, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            okText.rectTransform.Stretch();

            root.gameObject.SetActive(false);
        }

        /// <param name="cancelLabel">null for a single-button notice.</param>
        public void Show(string titleText, string bodyText, string cancelLabel, string okLabel, Action confirm)
        {
            title.text = titleText;
            body.text = bodyText;
            onOk = confirm;
            cancel.gameObject.SetActive(cancelLabel != null);
            cancelText.text = cancelLabel ?? "";
            okText.text = okLabel;
            var okRt = ok.rectTransform;
            okRt.anchorMin = new Vector2(cancelLabel != null ? 0.5f : 0f, 0);
            okRt.offsetMin = new Vector2(cancelLabel != null ? Metrics.Pad * 0.4f : Metrics.Pad * 1.4f, okRt.offsetMin.y);
            Color c = confirm != null ? Theme.Warn : Theme.SurfaceHi;
            SetButtonColors(ok.GetComponent<Button>(), c, Lighten(c, 0.1f), Darken(c, 0.2f));
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
        }

        public void Hide() => root.gameObject.SetActive(false);

        void Confirm()
        {
            var cb = onOk;
            Hide();
            cb?.Invoke();
        }
    }
}
