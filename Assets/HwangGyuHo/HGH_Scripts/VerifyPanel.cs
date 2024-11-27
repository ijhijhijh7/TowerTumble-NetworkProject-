using Firebase.Auth;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VerifyPanel : MonoBehaviour
{
    [SerializeField] NickNamePanel nickNamePanel;
    private Coroutine checkVerifyRoutine;
    private void OnEnable()
    {
        SendVerifyMail();
    }

    private void OnDisable()
    {
        if(checkVerifyRoutine != null)
        {
            StopCoroutine(checkVerifyRoutine);
        }
    }

    private void SendVerifyMail()
    {
        FirebaseUser user = BackendManager.Auth.CurrentUser;
        user.SendEmailVerificationAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("SendEmailVerificationAsync was canceled.");
                gameObject.SetActive(false);
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SendEmailVerificationAsync encountered an error: " + task.Exception);
                gameObject.SetActive(false);
                return;
            }

            Debug.Log("Email sent successfully.");
            checkVerifyRoutine = StartCoroutine(CheckVerifyRoutine());
        });

    }
    
    /// <summary>
    /// 3�ʸ��� ����Ȯ���ϴ� �ڷ�ƾ
    /// </summary>
    /// <returns></returns>
    IEnumerator CheckVerifyRoutine()
    {
        WaitForSeconds delay = new WaitForSeconds(3f);

        while (true)
        {
            BackendManager.Auth.CurrentUser.ReloadAsync().ContinueWithOnMainThread(task => 
            {
                if (task.IsCanceled)
                {
                    Debug.LogError("ReloadAsync was canceled.");
                    return;
                }
                if (task.IsFaulted)
                {
                    Debug.LogError($"ReloadAsync encountered an error: {task.Exception.Message}");
                    return;
                }

                if (BackendManager.Auth.CurrentUser.IsEmailVerified == true)
                {
                    Debug.Log("���� Ȯ��");
                    nickNamePanel.gameObject.SetActive(true);
                    gameObject.SetActive(false);
                }
            });
            // 3���� ���� ������ ����
            yield return delay;
        }
    }
}
