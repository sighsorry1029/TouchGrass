using UnityEngine;

namespace TouchGrass;

internal sealed class TrainingDummyNightAggro : MonoBehaviour
{
    private const float DetectionRange = 16f;
    private const float DetectionInterval = 0.5f;
    private const float StopDistance = 1.8f;
    private const float SlideSpeed = 3f;
    private const float TurnSpeed = 720f;

    private Character? _character;
    private ZNetView? _netView;
    private Rigidbody? _body;
    private ZSyncTransform? _syncTransform;
    private Player? _target;
    private float _detectionTimer;

    private void Awake()
    {
        _character = GetComponent<Character>();
        _netView = _character != null && _character.m_nview != null ? _character.m_nview : TrainingDummyIdentity.GetNetView(this);
        _body = GetComponent<Rigidbody>();
        _syncTransform = GetComponent<ZSyncTransform>();
    }

    private void FixedUpdate()
    {
        if (!HasOwnership())
        {
            return;
        }

        if (!CanActAtNight())
        {
            _target = null;
            StopOwnedMotion();
            return;
        }

        UpdateTarget(Time.fixedDeltaTime);
        if (_target == null)
        {
            StopOwnedMotion();
            return;
        }

        Vector3 toTarget = _target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            StopOwnedMotion();
            return;
        }

        Vector3 direction = toTarget / distance;
        LookTowards(direction);
        SlideTowardsTarget(direction, distance);
    }

    private bool HasOwnership()
    {
        return _netView != null &&
               _netView.IsValid() &&
               _netView.IsOwner();
    }

    private bool CanActAtNight()
    {
        return TouchGrassPlugin._trainingDummyNightAggro.Value == TouchGrassPlugin.Toggle.On &&
               EnvMan.instance != null &&
               EnvMan.IsNight() &&
               _character != null &&
               !_character.IsDead();
    }

    private Player? FindClosestTarget()
    {
        Player? closest = null;
        float closestDistanceSq = DetectionRange * DetectionRange;
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player == null ||
                player.IsDead() ||
                player.InDebugFlyMode() ||
                player.InGhostMode())
            {
                continue;
            }

            Vector3 delta = player.transform.position - transform.position;
            delta.y = 0f;
            float distanceSq = delta.sqrMagnitude;
            if (distanceSq <= closestDistanceSq)
            {
                closest = player;
                closestDistanceSq = distanceSq;
            }
        }

        return closest;
    }

    private void UpdateTarget(float dt)
    {
        _detectionTimer -= dt;
        if (_detectionTimer > 0f && IsValidTarget(_target))
        {
            return;
        }

        _target = FindClosestTarget();
        _detectionTimer = DetectionInterval;
    }

    private bool IsValidTarget(Player? player)
    {
        if (player == null ||
            player.IsDead() ||
            player.InDebugFlyMode() ||
            player.InGhostMode())
        {
            return false;
        }

        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= DetectionRange * DetectionRange;
    }

    private void LookTowards(Vector3 direction)
    {
        if (_character != null)
        {
            _character.SetLookDir(direction);
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        if (_body != null)
        {
            Quaternion rotation = Quaternion.RotateTowards(_body.rotation, targetRotation, TurnSpeed * Time.fixedDeltaTime);
            _body.MoveRotation(rotation);
            return;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, TurnSpeed * Time.fixedDeltaTime);
    }

    private void SlideTowardsTarget(Vector3 direction, float distance)
    {
        float remaining = distance - StopDistance;
        if (remaining <= 0f)
        {
            StopOwnedMotion();
            return;
        }

        Vector3 nextPosition = transform.position + direction * Mathf.Min(remaining, SlideSpeed * Time.fixedDeltaTime);
        if (_body != null)
        {
            _body.MovePosition(nextPosition);
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = nextPosition;
        }

        _syncTransform?.SyncNow();
    }

    private void OnDisable()
    {
        _target = null;
        if (HasOwnership())
        {
            StopOwnedMotion();
        }
    }

    private void StopOwnedMotion()
    {
        _character?.SetMoveDir(Vector3.zero);
        if (_body == null)
        {
            return;
        }

        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;
    }
}
