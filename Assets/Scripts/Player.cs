using UnityEngine;
using System.Linq;

public class Player : MonoBehaviour
{
    public Sprite[] sprites;
    public float strength = 5f;
    public float gravity = -9.81f;
    public float tilt = 5f;

    private SpriteRenderer spriteRenderer;
    private Vector3 direction;
    private int spriteIndex;

    // Mikrofon
    private AudioClip microphoneClip;
    private string micDevice;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // Kanat animasyonu
        InvokeRepeating(nameof(AnimateSprite), 0.15f, 0.15f);

        // Mikrofon başlat
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            microphoneClip = Microphone.Start(micDevice, true, 10, 16000);
            Debug.Log("Mikrofon başlatıldı: " + micDevice);
        }
        else
        {
            Debug.LogWarning("Hiç mikrofon bulunamadı!");
        }
    }

    private void OnEnable()
    {
        Vector3 position = transform.position;
        position.y = 0f;
        transform.position = position;
        direction = Vector3.zero;
    }

    private void Update()
    {
        float blowStrength = GetBlowFiltered();

        if (blowStrength > 0.08f) // üfleme eşiği
        {
            direction = Vector3.up * strength;
        }

        // Yerçekimi
        direction.y += gravity * Time.deltaTime;
        transform.position += direction * Time.deltaTime;

        // Eğimi ayarla
        Vector3 rotation = transform.eulerAngles;
        rotation.z = direction.y * tilt;
        transform.eulerAngles = rotation;
    }

    private void AnimateSprite()
    {
        spriteIndex++;
        if (spriteIndex >= sprites.Length)
            spriteIndex = 0;
        spriteRenderer.sprite = sprites[spriteIndex];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Obstacle"))
            GameManager.Instance.GameOver();
        else if (other.gameObject.CompareTag("Scoring"))
            GameManager.Instance.IncreaseScore();
    }

    // -------------------------------
    // ÜFLEME ALGILAYICI (KONUŞMA FİLTRELİ)
    // -------------------------------
    private float GetBlowFiltered()
    {
        if (microphoneClip == null || Microphone.GetPosition(micDevice) <= 0)
            return 0;

        int sampleCount = 1024;
        float[] samples = new float[sampleCount];
        int micPos = Microphone.GetPosition(micDevice) - sampleCount;
        if (micPos < 0) return 0;
        microphoneClip.GetData(samples, micPos);

        // 1️⃣ RMS (genel ses gücü)
        float rms = Mathf.Sqrt(samples.Average(s => s * s));

        // 2️⃣ Basit frekans ayrımı (örnek farkı)
        float highFreqEnergy = 0f;
        float lowFreqEnergy = 0f;
        for (int i = 1; i < samples.Length; i++)
        {
            float diff = Mathf.Abs(samples[i] - samples[i - 1]);
            if (i % 4 == 0)  // düşük frekans
                lowFreqEnergy += diff;
            else              // yüksek frekans
                highFreqEnergy += diff;
        }

        // 3️⃣ Tiz oranı hesapla
        float total = highFreqEnergy + lowFreqEnergy + 1e-6f;
        float highFreqRatio = highFreqEnergy / total;

        // 4️⃣ Filtre: ses güçlü VE yüksek frekans baskınsa = üfleme
        if (rms > 0.03f && highFreqRatio > 0.65f)
        {
            return rms * highFreqRatio;
        }

        return 0;
    }
}
