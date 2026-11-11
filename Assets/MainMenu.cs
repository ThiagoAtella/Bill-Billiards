using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Método para o botão "Jogar"
    public void PlayGame()
    {
        // Carrega a próxima cena na fila do Build Settings
        // Você também pode usar o nome exato da cena: SceneManager.LoadScene("Fase1");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // Método para o botão "Sair"
    public void QuitGame()
    {
        Debug.Log("O jogo fechou!"); // O Unity Editor não fecha, então usamos o Debug para testar
        Application.Quit();
    }
}

