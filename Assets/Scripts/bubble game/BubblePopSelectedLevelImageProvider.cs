using System.Collections.Generic;
using UnityEngine;

public class BubblePopSelectedLevelImageProvider : MonoBehaviour
{
    [SerializeField] private BubblePopGameManager gameManager;
    [SerializeField] private List<Sprite> levelImages = new List<Sprite>();
    [SerializeField] private bool startGameAfterApply = false;

    public List<Sprite> LevelImages => levelImages;

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

        // Note: Do not swap or override gameManager's contentSprites from levelImages
        gameManager.BeginLevel(BubblePopLevelMenu.GetSelectedLevelIndex());
    }
}
