using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenu : MonoBehaviour
{
    public GameObject creditsPanel;

    private void Update()
    {
        if (creditsPanel != null && creditsPanel.activeSelf)
        {
            bool escapePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escapePressed = true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escapePressed = true;
            }
#endif
            if (escapePressed)
            {
                CloseCredits();
            }
        }
    }

    // Método para o botão "Jogar"
    public void PlayGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // Método para abrir o painel de Créditos
    public void OpenCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
        }
    }

    // Método para fechar o painel de Créditos
    public void CloseCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }

    // Método para o botão "Sair"
    public void QuitGame()
    {
        Debug.Log("O jogo fechou!");
        Application.Quit();
    }
}
