using System.Collections.Generic;
using UnityEngine;

public class BubblePopSelectedLevelImageProvider : MonoBehaviour
{
    [SerializeField] private BubblePopGameManager gameManager;
    [SerializeField] private List<Sprite> levelImages = new List<Sprite>();
    [SerializeField] private bool startGameAfterApply = false;

    private bool hasStartedGame;

    private void Awake()
    {
        ApplySelectedLevelImage();
    }

    private void OnEnable()
    {
        ApplySelectedLevelImage();

        if (startGameAfterApply && !hasStartedGame && gameManager != null)
        {
            hasStartedGame = true;
            gameManager.StartGame();
        }
    }

    [ContextMenu("Apply Selected Level Image")]
    public void ApplySelectedLevelImage()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<BubblePopGameManager>();
        }

        if (gameManager == null)
        {
            return;
        }

        int selectedLevelIndex = BubblePopLevelMenu.GetSelectedLevelIndex();
        if (selectedLevelIndex < 0 || selectedLevelIndex >= levelImages.Count || levelImages[selectedLevelIndex] == null)
        {
            return;
        }

        gameManager.SetContentSprites(new[] { levelImages[selectedLevelIndex] });
    }
}
