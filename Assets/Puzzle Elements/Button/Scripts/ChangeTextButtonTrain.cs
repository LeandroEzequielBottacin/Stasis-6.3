using TMPro;
using UnityEngine;

public class ChangeTextButtonTrain : MonoBehaviour
{
    [SerializeField]private string textOff;
    [SerializeField]private string textOn;


    [SerializeField]private TextMeshProUGUI _text;

    private void Start()
    {
        _text.text = textOff;
    }
    public void ActivateButtonTrain()
    {
        _text.text = textOn;
    }
    public void DesactivateButtonTrain()
    {
        _text.text = textOff;
    }
}
