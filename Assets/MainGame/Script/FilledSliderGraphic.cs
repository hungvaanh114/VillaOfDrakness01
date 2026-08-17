using UnityEngine;
using UnityEngine.UI;

public class FilledSliderGraphic : MonoBehaviour
{
    public Slider slider;
    public Image fillImage;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        if (slider != null)
            slider.onValueChanged.AddListener(UpdateFill);

        UpdateFill(slider != null ? slider.value : 0f);
    }

    private void OnEnable()
    {
        UpdateFill(slider != null ? slider.value : 0f);
    }

    private void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(UpdateFill);
    }

    public void UpdateFill(float value)
    {
        if (slider == null || fillImage == null)
            return;

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = Mathf.InverseLerp(slider.minValue, slider.maxValue, value);
    }
}
