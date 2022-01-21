using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Microsoft.MixedReality.Toolkit.Experimental.UI
{
    /// <summary>
    /// This component links the NonNativeKeyboard to a TMP_InputField
    /// Put it on the TMP_InputField and assign the NonNativeKeyboard.prefab
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class InputObject : MonoBehaviour, IPointerDownHandler
    {
        /*[Experimental]
        [SerializeField] private NonNativeKeyboard keyboard = null;*/

        public NonNativeKeyboard keyboard;
        public InputExample ipInput;


        public void OnPointerDown(PointerEventData eventData)
        {
            ipInput.OnFocusCanceled();

            keyboard.PresentKeyboard(GetComponent<TMP_InputField>().text);

            keyboard.OnClosed += DisableKeyboard;
            keyboard.OnTextSubmitted += DisableKeyboard;
            keyboard.OnTextUpdated += UpdateText;
        }

        private void UpdateText(string text)
        {
            GetComponent<TMP_InputField>().text = text;
        }

        private void DisableKeyboard(object sender, EventArgs e)
        {
            keyboard.OnTextUpdated -= UpdateText;
            keyboard.OnClosed -= DisableKeyboard;
            keyboard.OnTextSubmitted -= DisableKeyboard;

            keyboard.Close();
        }

        private void OnDestroy()
        {
            if (keyboard != null && keyboard.isActiveAndEnabled == true)
            {
                keyboard.OnTextUpdated -= UpdateText;
                keyboard.OnClosed -= DisableKeyboard;
                keyboard.OnTextSubmitted -= DisableKeyboard;

                keyboard.Close();
            }
        }

        public void OnFocusCanceled()
        {
            if (keyboard != null && keyboard.isActiveAndEnabled == true)
            {
                keyboard.OnTextUpdated -= UpdateText;
                keyboard.OnClosed -= DisableKeyboard;
                keyboard.OnTextSubmitted -= DisableKeyboard;
                
                keyboard.Close();
            }
            //GetComponent<TMP_InputField>().text = _text;
        }
    }
}