using System;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The start screen on the scoreboard: the instructions, and behind the round "i" button an About page (the game,
    /// how it is built and who made it) until the player presses "Back". It opens on the instructions every time it
    /// appears. While the About page is open the trigger only presses buttons, so reading it never starts the game.
    /// </summary>
    public class StartScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject _instructionsPage;
        [SerializeField] private GameObject _aboutPage;
        [SerializeField] private ARButton _aboutButton;
        [SerializeField] private ARButton _backButton;

        /// <summary>True while the About page is shown instead of the instructions.</summary>
        public bool ShowingAbout => _aboutPage.activeSelf;

        /// <summary>Raised when the player opens the About page.</summary>
        public event Action OnAboutOpened;

        private void Awake()
        {
            _aboutButton.OnPressed += HandleAboutPressed;
            _backButton.OnPressed += HandleBackPressed;
        }

        private void OnDestroy()
        {
            _aboutButton.OnPressed -= HandleAboutPressed;
            _backButton.OnPressed -= HandleBackPressed;
        }

        private void OnEnable()
        {
            ShowAbout(false);
        }

        /// <summary>Shows the About page, or the instructions.</summary>
        public void ShowAbout(bool about)
        {
            _instructionsPage.SetActive(!about);
            _aboutPage.SetActive(about);
        }

        private void HandleAboutPressed()
        {
            ShowAbout(true);
            OnAboutOpened?.Invoke();
        }

        private void HandleBackPressed()
        {
            ShowAbout(false);
        }
    }
}
