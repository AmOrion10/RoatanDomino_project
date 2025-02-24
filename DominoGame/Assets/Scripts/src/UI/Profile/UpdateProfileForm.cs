using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using TMPro;

using UnityEngine;

public class UpdateProfileForm : MonoBehaviour
{
    public TMP_InputField LoginInputField;
    public TMP_InputField EmailInputField;
    public TMP_InputField BirthDateInputField;
    public TMP_Dropdown NationalityDropDown;

    public void SetUp(UserDto user)
    {
        LoadCountries();
        LoginInputField.text = user.Login;
        EmailInputField.text = user.Email;
        BirthDateInputField.text = user.BirthDate;
        if (user.Nationality != null)
        {
            int index = NationalityDropDown.options.FindIndex(o => o.text.Equals(user.Nationality));
            if (index >= 0)
            {
                NationalityDropDown.value = index;
                NationalityDropDown.RefreshShownValue();
            }
        }
    }

    private void LoadCountries()
    {
        NationalityDropDown.ClearOptions();
        List<string> countries = Countries.GetAllCountries();
        NationalityDropDown.AddOptions(countries);
    }

    public void ShowForm()
    {
        LoginInputField.gameObject.SetActive(true);
        //EmailInputField.gameObject.SetActive(true);
        NationalityDropDown.gameObject.SetActive(true);
        BirthDateInputField.gameObject.SetActive(true);
    }

    public void HideForm()
    {
        LoginInputField.gameObject.SetActive(false);
        //EmailInputField.gameObject.SetActive(false);
        NationalityDropDown.gameObject.SetActive(false);
        BirthDateInputField.gameObject.SetActive(false);
    }
}
