using AlienInvaders.Core;
using AlienInvaders.Core.Motion;
using UnityEngine;

namespace AlienInvaders.Motion
{
    /// <summary>
    /// Placeholder for the future motion platform (2 or 3 actuators driven by an ESP32 over Wi-Fi). It is disabled in
    /// the MVP and only shows where the hardware plugs in: because it implements IMotionOutput, connecting the
    /// platform means filling in Apply and enabling this component, without touching any game rule.
    /// Planned packet (UDP, compact JSON v1, 20 to 50 Hz):
    ///   { "v": 1, "seq": 1234, "t": 52.35, "pitch": 6.2, "roll": -3.1, "heave": -0.4, "shake": 0.3, "estop": false }
    /// Safety also lives in the firmware: hard limits (20 degrees, 15 degrees per second), a 200 ms watchdog that
    /// levels the platform when packets stop, and a physical emergency stop.
    /// </summary>
    public class PlatformMotionOutput : MonoBehaviour, IMotionOutput
    {
        [SerializeField] private string _host = "192.168.4.1";
        [SerializeField] private int _port = 7777;
        [Tooltip("0..1: scales the design tilt down to what the hardware can do safely.")]
        [Range(0f, 1f)]
        [SerializeField] private float _platformScale = 0.5f;

        /// <summary>Address of the ESP32 on the local network.</summary>
        public string Host => _host;

        /// <summary>UDP port the ESP32 listens on.</summary>
        public int Port => _port;

        /// <summary>Share of the design tilt sent to the hardware.</summary>
        public float PlatformScale => _platformScale;

        /// <summary>Future: scale the sample by PlatformScale and send the packet above with a UdpClient.</summary>
        public void Apply(MotionSample sample)
        {
            // TODO (future platform): build the JSON packet with an increasing seq number and send it to Host:Port.
        }

        /// <summary>Future: send a level packet (and "estop": true when the game pauses, quits or loses tracking).</summary>
        public void ResetToLevel()
        {
            // TODO (future platform): send pitch = roll = heave = 0.
        }
    }
}
