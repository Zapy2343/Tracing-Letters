using System.Collections.Generic;
using UnityEngine;

public class BubblePopSelectedLevelImageProvider : MonoBehaviour
{
    [SerializeField] private BubblePopGameManager gameManager;
    [SerializeField] private List<Sprite> levelImages = new List<Sprite>();

    private void Awake()
    {
        ApplySelectedLevelImage();
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
