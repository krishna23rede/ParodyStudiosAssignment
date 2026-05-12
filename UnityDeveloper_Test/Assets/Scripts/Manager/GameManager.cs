using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum GameOverReason
{
    TimerExpired,
    Fell
}

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        // Keep only one instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [Header("Timer")]
    public float timeLimit = 120f;

    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI cubeCountText;
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverReasonText;

    [Header("Cubes")]
    public CollectibleCube[] allCubes;

    private float timeRemaining;
    private int cubesCollected = 0;
    private int totalCubes;
    private bool gameOver = false;

    void Start()
    {
        // Initialize timer
        timeRemaining = timeLimit;

        // Auto-find cubes if empty
        if (allCubes == null || allCubes.Length == 0)
            allCubes = FindObjectsByType<CollectibleCube>(FindObjectsSortMode.None);

        totalCubes = allCubes.Length;

        // Hide game over UI
        if(gameOverPanel) gameOverPanel.SetActive(false);

        UpdateCubeUI();
    }

    void Update()
    {
        if (gameOver) return;

        // Countdown timer
        timeRemaining -= Time.deltaTime;

        // Trigger game over when timer ends
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            TriggerGameOver(GameOverReason.TimerExpired);
        }

        UpdateTimerUI();
    }

    // Called when cube is collected
    public void RegisterCubeCollected()
    {
        if (gameOver) return;

        cubesCollected++;

        UpdateCubeUI();

        // Win when all cubes collected
        if (cubesCollected >= totalCubes)
            TriggerWin();
    }

    // Shows game over screen
    public void TriggerGameOver(GameOverReason reason)
    {
        if (gameOver) return;

        gameOver = true;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.Confined;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // Set failure message
        if (gameOverReasonText != null)
        {
            gameOverReasonText.text = reason switch
            {
                GameOverReason.TimerExpired => "Time's up!",
                GameOverReason.Fell         => "You fell into the void!",
                _                           => "Game Over"
            };
        }
    }

    // Shows win screen
    void TriggerWin()
    {
        gameOver = true;

        Time.timeScale = 0f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverReasonText != null)
            gameOverReasonText.text = "You collected all cubes!";
    }

    // Updates timer text
    void UpdateTimerUI()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);

        timerText.text = $"{minutes:00}:{seconds:00}";

        // Turn red under 20 seconds
        timerText.color = timeRemaining < 20f ? Color.red : Color.white;
    }

    // Updates cube counter text
    void UpdateCubeUI()
    {
        if (cubeCountText != null)
            cubeCountText.text = $"Cubes: {cubesCollected} / {totalCubes}";
    }

    // Reload current scene
    public void RestartScene()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}