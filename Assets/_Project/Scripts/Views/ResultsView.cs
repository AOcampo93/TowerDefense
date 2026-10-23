using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AlienInvaders.Core;
using TMPro;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// End of match screen in the real room (AR): a panel floating 1.5 m in front of the player with "Your run" on
    /// the left (time, ships, accuracy, wave, score) and the local Top 10 on the right, plus a "Play again" button.
    /// It only shows what the GameController gives it; saving records is not its job.
    /// </summary>
    public class ResultsView : MonoBehaviour
    {
        [Tooltip("Canvas root, hidden outside the Results state.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _head;
        [SerializeField] private float _distance = 1.5f;
        [SerializeField] private float _heightOffset = -0.1f;
        [SerializeField] private TMP_Text _runText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _tableText;
        [SerializeField] private ARButton _playAgain;
        [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.2f);

        /// <summary>Raised when the player presses "Play again".</summary>
        public event Action OnPlayAgain;

        private void Awake()
        {
            _panel.SetActive(false);
            _playAgain.OnPressed += HandlePlayAgainPressed;
        }

        private void OnDestroy()
        {
            _playAgain.OnPressed -= HandlePlayAgainPressed;
        }

        /// <summary>Shows the panel in front of the player with this match and the current table.</summary>
        /// <param name="highlightIndex">Row of the table to highlight (this match), or -1.</param>
        public void Show(MatchResult result, IReadOnlyList<MatchResult> table, int highlightIndex)
        {
            PlaceInFrontOfPlayer();
            _runText.text =
                "<size=85%><color=#9FB4C7>YOUR RUN</color></size>\n" +
                $"Time   {FormatTime(result.SecondsSurvived)}\n" +
                $"Ships   {result.ShipsDestroyed}\n" +
                $"Accuracy   {Mathf.RoundToInt(result.Accuracy * 100f)}%\n" +
                $"Wave   {result.WaveReached}";
            _scoreText.text = "<size=50%><color=#9FB4C7>SCORE</color></size>\n" + result.Score.ToString(CultureInfo.InvariantCulture);
            ShowTable(table, highlightIndex);
            _panel.SetActive(true);
        }

        /// <summary>Rewrites the Top 10 (after a new record is added).</summary>
        public void ShowTable(IReadOnlyList<MatchResult> table, int highlightIndex)
        {
            var text = new StringBuilder("<size=85%><color=#9FB4C7>TOP 10</color></size>\n");
            if (table.Count == 0)
            {
                text.Append("<size=85%>No records yet</size>");
            }

            string highlight = ColorUtility.ToHtmlStringRGB(_highlightColor);
            for (int i = 0; i < table.Count; i++)
            {
                MatchResult entry = table[i];
                // Fixed-width digits so the columns line up with a proportional font.
                string row = string.Format(CultureInfo.InvariantCulture, "<mspace=0.6em>{0,2}  {1,-3}  {2,6}  {3,5}  W{4}</mspace>",
                    i + 1, entry.Initials, entry.Score, FormatTime(entry.SecondsSurvived), entry.WaveReached);
                text.Append(i == highlightIndex ? $"<color=#{highlight}>{row}</color>" : row).Append('\n');
            }
            _tableText.text = text.ToString();
        }

        /// <summary>Shows or hides the "Play again" button.</summary>
        public void SetPlayAgainVisible(bool visible)
        {
            _playAgain.gameObject.SetActive(visible);
        }

        /// <summary>Removes the panel.</summary>
        public void Hide()
        {
            _panel.SetActive(false);
        }

        private void HandlePlayAgainPressed()
        {
            OnPlayAgain?.Invoke();
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
