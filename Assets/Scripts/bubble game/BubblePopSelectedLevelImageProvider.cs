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

        if (levelImages != null && levelImages.Count > 0)
        {
            gameManager.SetContentSprites(levelImages);
        }

        gameManager.BeginLevel(BubblePopLevelMenu.GetSelectedLevelIndex());
    }
}
