using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class Blocks : MonoBehaviour
{
    [SerializeField] private float basicFallSpeed;          // �ϰ� �ӵ�
    [SerializeField] private float fastFallSpeed;           // ���� �ϰ� �ӵ�
    [SerializeField] private GameObject spotlightPrefab;    // spotlight UI ������Ʈ
    [SerializeField] private float moveAmount;              // �Ϲ� �̵���
    [SerializeField] private float pushAmount;              // ��ġ�� �̵���
    [SerializeField] private float rotateAmount;            // ȸ����
    [SerializeField] private float moveDelay;               // �̵� �� �ο��� ������

    [SerializeField] private GameObject[] tiles;            // ���� Ÿ�ϵ�
    [SerializeField] private Transform tileParent;          // Ÿ�ϵ��� �θ� Ʈ������
    [SerializeField] private Vector2[] tileRotPos;          // ȸ���� ������ ��ġ����

    [SerializeField] LayerMask castLayer; 

    private int rotIndex = 0;               // tileRotPos �迭���� ����� index��

    private Rigidbody2D rigid;              // Rigidbody2D ������Ʈ ����                                          

    private Vector2 currentVelocity;        // ���� �ϰ� �ӵ�
    private Vector2 currentDirection;       // ���� �̵� ����
    private float currentAmount;            // ���� �̵���

    private bool isControllable;            // ���� ���� ����
    private bool isFastDown;                // ���� �ϰ� ����
    private bool isPushing;                 // ��ġ�� ����

    private Coroutine moveRoutine;          // �̵� �� ����� �ڷ�ƾ
    private WaitForSeconds wsMoveDelay;     // �̵� �ڷ�ƾ���� Ȱ���� WaitForSeconds ��ü

    public UnityAction OnBlockFallen;       // ������ �ʹ����� ������ �� Invoke (ü�°���, ��� ó���� Ȱ��)
    public UnityAction OnBlockEntered;      // ������ �������� �� Invoke (���� ī���ÿ� Ȱ��)
    public UnityAction OnBlockExited;       // ������ �����ߴٰ� ��� �� (���� ī���ÿ� Ȱ��)

    public bool IsEntered { get { return isControllable; } }     // �ܺο��� �������θ� Ȯ���ϱ� ���� ������Ƽ

    private void Awake()
    {
        // ������Ʈ ����
        rigid = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        isControllable = true;
        wsMoveDelay = new WaitForSeconds(moveDelay);

        // �⺻ �ϰ� �ӵ� ����
        currentVelocity = new Vector2(rigid.velocity.x, basicFallSpeed);
    }

    void Update()
    {
        // Ÿ���� ������ �ε��� ���ĺ��ʹ� �ش� ������ ���� ���� �Ұ���
        if (!isControllable)
        {
            return;
        }

        // �̵�, ��ġ�� ���� Ȯ���Ͽ� amount ����
        currentAmount = isPushing ? pushAmount : moveAmount;
        // �̵� or ��ġ�� ����
        ExecuteMove();

        // ���� �ϰ� ���� Ȯ���Ͽ� velocity ����
        currentVelocity.y = isFastDown ? fastFallSpeed : basicFallSpeed;
        // �ϰ�
        rigid.velocity = currentVelocity;

        // flag �ʱ�ȭ
        // �÷��̾ ���������� key�� ������ �ִ´ٸ� �ٽ� true�� �� ���̰�
        // �÷��̾ key�� ���̻� ������ �ʴ´ٸ� false�� ���·� ���ӵ� ���̴�.
        isPushing = false;
        isFastDown = false;
    }

    // Player�� ���� �ϰ� ������ ���� Interface
    public void Down()
    {
        isFastDown = true;
    }

    // Player�� �̵� ������ ���� Interface
    public void Move(Vector2 dir)
    {
        currentDirection = dir;
    }

    // Player�� ��ġ�� ������ ���� Interface
    public void Push(Vector2 dir)
    {
        currentDirection = dir;
        isPushing = true;
    }

    public void Rotate()
    {
        if (!isControllable)
            return;

        // z������ rotateAmount ��ŭ ȸ��
        transform.Rotate(Vector3.forward, rotateAmount);

        // ȸ���� ������ ��ġ���� �������� �ʾ����� ���⼭ return
        if (tileRotPos.Length == 0)
            return;

        // tile Parent�� ��ġ�� ����
        tileParent.localPosition = tileRotPos[rotIndex++];

        // 360�� ȸ���� ��� �����ߴٸ� �ٽ� ó������ 
        if (rotIndex >= tileRotPos.Length)
            rotIndex = 0;
    }

    // ���� �̵�, ��ġ�⸦ ����
    private void ExecuteMove()
    {
        // �̵��� ���� ���
        if (currentDirection == Vector2.zero)
        {
            // �̵� �ڷ�ƾ ����
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }
            return;
        }

        // ���� �̵� �ڷ�ƾ�� ������� �ʾҴٸ� ����
        if (moveRoutine == null)
        {
            moveRoutine = StartCoroutine(MoveRoutine());
        }
    }

    // �̵� �ڷ�ƾ�� ���� �̵� Ÿ�̹��� ����
    // Update���� �ٷ� �̵��ع����� �̵��ӵ� ��� �ȉ´�
    // �߰��� �¿� �̵� �� �浹 ���� �� �浹�� ó���Ѵ�.
    private IEnumerator MoveRoutine()
    {
        Vector2 movePos;
        Vector2 lastDir;
        float lastAmount;
        Vector2 toHit;
        RaycastHit2D[] result;
        Vector2 startPos;

        lastDir = currentDirection;
        // ��� ������ ���� ����
        while (isControllable)
        {
            movePos = currentDirection * currentAmount;
            lastDir = currentDirection;
            lastAmount = currentAmount;

            // �浹 ���� ����
            // position �̵� �ÿ� ������ġ�� �����Ǵ� ��ü�� ������ ���� �̵����� �ʰ� ���� ó��
            for (int i = 0; i < tiles.Length; i++)
            {
                // �� Ÿ���� ��ġ�κ��� �̵��� �������� raycast
                startPos = (Vector2)tiles[i].transform.position + lastDir * 0.25f;
                result = Physics2D.RaycastAll(startPos, lastDir, lastAmount-0.05f, castLayer);
                foreach(RaycastHit2D hit in result)
                {
                    // �θ� ������Ʈ�� hit�ȴٸ� �ش� Ÿ���� �߰��� ���� Ÿ���̹Ƿ� �Ѿ��.
                    if (hit.transform == transform)
                        break;

                    // ������� ����
                    isControllable = false;

                    // �浹 �˻縦 ������ ��ġ�� ���� �浹�� ���������� �Ÿ��� Ȯ��
                    toHit = hit.point - startPos;
                    // �浹�� ���������� ��ġ�� �� �پ����� �ʴ� ���
                    // (��ġ���� ��� ���� 2ĭ���� (1f) �����̱� ������ �ʿ��� ����)
                    if (toHit.magnitude > 0.01f)
                    {
                        Debug.Log("pos calibration");
                        // ������ ��ġ�� �������ش�.
                        transform.position += new Vector3(toHit.x, 0, 0);
                    }

                    // for debug
                    Debug.Log($"Horizontal Collision : {tiles[i].name} > {hit.collider.name}");
                    Debug.Log($"origin : {(Vector2)tiles[i].transform.position}, direction : {lastDir}");
                    SpriteRenderer sr = tiles[i].GetComponent<SpriteRenderer>();
                    sr.color = Color.red;

                    yield break;
                }
            }

            Debug.Log("No Collision");

            // ���������� �̵��ؾ� �ϹǷ� position���� �����Ѵ�.
            rigid.position += movePos;

            // delay��ŭ ���
            yield return wsMoveDelay;
        }

        // �ڷ�ƾ ���� �ʱ�ȭ
        moveRoutine = null;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (isControllable)
            rigid.velocity = Vector2.zero;

        // �浹�� ���� other�� �������� Ȯ��
        if (other.contacts[0].normal.y >= 0.9f)
        {
            Debug.Log("entered");

            //// �浹 �� flag ���� (���̾�� Ư�� ��ü ���� �ʿ�?)
            //isControllable = false;

            // �̺�Ʈ �߻�
            // (Ÿ���� ��� �����浹�� ������ �����ؾ� �ϴ°�?)
            OnBlockEntered?.Invoke();
        }
        else
        {
            Debug.Log("not entered");
        }
        // �浹 �� flag ���� (���̾�� Ư�� ��ü ���� �ʿ�?)
        isControllable = false;
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        // �̺�Ʈ �߻�
        // ���� ������ Enter ���¿������� Ȯ���� ������ �ʿ�? 
        OnBlockExited?.Invoke();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (tiles.Length <= 0)
            return;

        for (int i = 0; i < tiles.Length; i++)
        {
            Gizmos.DrawRay(tiles[i].transform.position+Vector3.left*0.25f, Vector3.left * 0.45f);
            Gizmos.DrawRay(tiles[i].transform.position+Vector3.right*0.25f, Vector3.right * 0.45f);
        }
    }
}