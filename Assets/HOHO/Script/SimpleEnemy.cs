using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleEnemy : MonoBehaviour
{
    public bool canBeKillWhenPlayerJumpOn = false;
    public float moveSpeed = 2;
    public float gravity = -9.8f;
    public int horizontalInput = -1;
    public LayerMask layerAsGround;
    public AudioClip soundDie;
    [ReadOnly] public bool isGrounded = false;
    CharacterController characterController;
    [ReadOnly] public Vector2 velocity;
    bool isDead = false;
    Animator anim;

    private void Start()
    {
        anim = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        transform.forward = new Vector3(horizontalInput, 0, 0);
        if (GameManager.Instance.gameState != GameManager.GameState.Playing)
            velocity.x = 0;
        else
            velocity.x = moveSpeed * horizontalInput;

        //velocity.x = moveSpeed * horizontalInput;      //calucating the x speed

        CheckGround();

        if (isGrounded && velocity.y < 0)
            velocity.y = 0;
        else
            velocity.y += gravity * Time.deltaTime;     //add gravity

        if (isDead)
            velocity.x = 0;

        Vector2 finalVelocity = velocity;
        if (isGrounded && groundHit.normal != Vector3.up)        //calulating new speed on slope
            GetSlopeVelocity(ref finalVelocity);

        characterController.Move(finalVelocity * Time.deltaTime);
        HandleAnimation();

        if (isWallAHead())
            Flip();
    }

    RaycastHit groundHit;
    void CheckGround()
    {
        isGrounded = false;
        if (Physics.SphereCast(transform.position + Vector3.up * 1, characterController.radius * 0.9f, Vector3.down, out groundHit, 1f, layerAsGround))
        {
            float distance = transform.position.y - groundHit.point.y;
            if (distance <= (characterController.skinWidth + 0.01f))
                isGrounded = true;
        }
    }

    void GetSlopeVelocity(ref Vector2 vel)
    {
        var crossSlope = Vector3.Cross(groundHit.normal, Vector3.forward);
        vel = vel.x * crossSlope;

        Debug.DrawRay(transform.position, crossSlope * 10);
    }

    void Flip()
    {
        horizontalInput *= -1;
    }

    bool isWallAHead()
    {
        if (Physics.CapsuleCast(transform.position + Vector3.up * characterController.height * 0.5f, transform.position + Vector3.up * (characterController.height - characterController.radius),
            characterController.radius, horizontalInput > 0 ? Vector3.right : Vector3.left, 0.1f, layerAsGround))
        {
            return true;
        }
        else
            return false;
    }

    void HandleAnimation()
    {
        anim.SetFloat("speed", Mathf.Abs(velocity.x));
        anim.SetBool("isDead", isDead);
    }

    public void Kill()
    {
        isDead = true;
        SoundManager.PlaySfx(soundDie);
        Destroy(gameObject, 2);
    }
}
