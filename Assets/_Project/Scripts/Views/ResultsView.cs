using System;
using System.Collections.Generic;
using System.Globalization;
using AlienInvaders.Core;
using AlienInvaders.Core.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienInvaders.Views
{
    /// <summary>
    /// End of match screen in the real room (AR): a "TOWER DOWN" scoreboard (a 3D model) floating 1.5 m in front of
    /// the player. Its screen shows "Your run" on the left (time, ships, accuracy, wave, score and a shareable record
    /// code) and the local Top 10 on the right, one row per record on alternating stripes, with this match's row in
    /// gold. A new record first asks for initials under the score; then the "Play again" button takes their place,
    /// with a round feedback button beside it that turns the screen into the feedback form (FeedbackView) until the
    /// form is sent or closed. It only shows what the GameController gives it; saving records is not its job.
    /// </summary>
    public class ResultsView : MonoBehaviour
    {
        [Tooltip("Canvas root, hidden outside the Results state.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _head;
        [SerializeField] private float _distance = 1.5f;
        [SerializeField] private float _heightOffset = -0.1f;
        [Tooltip("Values of the run (time, ships, accuracy, wave), one per line beside their fixed labels.")]
        [SerializeField] private TMP_Text _runValues;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _codeText;
        [Tooltip("The ten rows of the Top 10, top to bottom, and the stripe behind each one.")]
        [SerializeField] private TMP_Text[] _rows;
        [SerializeField] private Image[] _rowBacks;
        [SerializeField] private InitialsPicker _picker;
        [SerializeField] private ARButton _playAgain;
        [Tooltip("Everything of this match's results; hidden while the feedback form is open.")]
        [SerializeField] private GameObject _resultsPage;
        [SerializeField] private FeedbackView _feedback;
        [SerializeField] private ARButton _feedbackButton;
        [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color _rowColor = Color.white;
        [SerializeField] private Color _stripeColor = new Color(0.35f, 0.42f, 0.5f, 0.18f);
        [SerializeField] private Color _plainRowColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color _highlightStripeColor = new Color(0.85f, 0.6f, 0.1f, 0.45f);

        /// <summary>Raised when the player presses "Play again".</summary>
        public event Action OnPlayAgain;

        /// <summary>Raised with the initials once the player confirms them.</summary>
        public event Action<string> OnInitialsConfirmed;

        private void Awake()
        {
            _panel.SetActive(false);
            _playAgain.OnPressed += HandlePlayAgainPressed;
            _feedbackButton.OnPressed += HandleFeedbackPressed;
            _feedback.OnClosed += HandleFeedbackClosed;
            _picker.OnConfirmed += HandleInitialsConfirmed;
        }

        private void OnDestroy()
        {
            _playAgain.OnPressed -= HandlePlayAgainPressed;
            _feedbackButton.OnPressed -= HandleFeedbackPressed;
            _feedback.OnClosed -= HandleFeedbackClosed;
            _picker.OnConfirmed -= HandleInitialsConfirmed;
        }

        /// <summary>Shows the panel in front of the player with this match and the current table.</summary>
        /// <param name="highlightIndex">Row of the table to highlight (this match), or -1.</param>
        public void Show(MatchResult result, IReadOnlyList<MatchResult> table, int highlightIndex)
        {
            PlaceInFrontOfPlayer();
            _runValues.text =
                $"{FormatTime(result.SecondsSurvived)}\n" +
                $"{result.ShipsDestroyed}\n" +
                $"{Mathf.RoundToInt(result.Accuracy * 100f)}%\n" +
                $"{result.WaveReached}";
            _scoreText.text = "SCORE: <size=140%>" + result.Score.ToString(CultureInfo.InvariantCulture) + "</size>";
            _codeText.text = "RECORD CODE: " + GroupedCode(PackedRecordCode.FromResult(result));
            _picker.Hide();
            ShowTable(table, highlightIndex);
            ShowFeedback(false);
            _panel.SetActive(true);
        }

        /// <summary>Rewrites the Top 10 (after a new record is added).</summary>
        public void ShowTable(IReadOnlyList<MatchResult> table, int highlightIndex)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                bool highlighted = i == highlightIndex;
                _rows[i].color = highlighted ? _highlightColor : _rowColor;
                _rowBacks[i].color = highlighted ? _highlightStripeColor : i % 2 == 0 ? _stripeColor : _plainRowColor;
                _rows[i].text = i < table.Count ? Row(i + 1, table[i]) : string.Empty;
            }
            if (table.Count == 0)
            {
                _rows[0].text = "<pos=8%>No records yet";
            }
        }

        // One row of the table. Each column starts at a fixed place, and the numbers use fixed-width digits padded on
        // the left, so they line up on the right like a scoreboard.
        private static string Row(int rank, MatchResult entry)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "<mspace=0.6em>{0,2}</mspace><pos=17%>{1}<pos=36%><mspace=0.6em>{2,6}</mspace><pos=62%><mspace=0.6em>{3,5}</mspace><pos=86%>W{4}",
                rank, entry.Initials, entry.Score, FormatTime(entry.SecondsSurvived), entry.WaveReached);
        }

        /// <summary>Asks for the initials of a new record (the "Play again" button waits until they are confirmed).</summary>
        public void BeginInitials(string suggestion)
        {
            SetPlayAgainVisible(false);
            _picker.Begin(suggestion);
        }

        /// <summary>Shows or hides the "Play again" button and the feedback button beside it.</summary>
        public void SetPlayAgainVisible(bool visible)
        {
            _playAgain.gameObject.SetActive(visible);
            _feedbackButton.gameObject.SetActive(visible);
        }

        /// <summary>Turns the screen into the feedback form, or back into this match's results.</summary>
        public void ShowFeedback(bool feedback)
        {
            _resultsPage.SetActive(!feedback);
            _feedback.gameObject.SetActive(feedback);
        }

        /// <summary>Removes the panel.</summary>
        public void Hide()
        {
            _picker.Hide();
            _panel.SetActive(false);
        }

        private void HandleInitialsConfirmed(string initials)
        {
            _picker.Hide();
            OnInitialsConfirmed?.Invoke(initials);
        }

        // 16 hex digits in groups of four, easier to read aloud or copy: 0000 0B0E 0007 1A2B.
        private static string GroupedCode(PackedRecordCode code)
        {
            string hex = code.ToHex();
            return $"{hex.Substring(0, 4)} {hex.Substring(4, 4)} {hex.Substring(8, 4)} {hex.Substring(12, 4)}";
        }

        private void HandlePlayAgainPressed()
        {
            OnPlayAgain?.Invoke();
        }

        private void HandleFeedbackPressed()
        {
            ShowFeedback(true);
        }

        private void HandleFeedbackClosed()
        {
            ShowFeedback(false);
        }

        // In front of the player at the moment the panel appears; it stays there, like an object in the room.
        private void PlaceInFrontOfPlayer()
        {
            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            _panel.transform.SetPositionAndRotation(
                _head.position + forward * _distance + Vector3.up * _heightOffset,
                Quaternion.LookRotation(forward));
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", total / 60, total % 60);
        }
    }
}
