﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralClimbing
{
    public class FreeClimb : MonoBehaviour {
        public Animator anim;
        public bool isClimbing;
        public bool isMid;

        bool inPosition;
        bool isLerping;
        float t;
        float delta;
        Vector3 startPos;
        Vector3 targetPos;
        Quaternion startRot;
        Quaternion targetRot;
        public float positionOffset = 1.0f;
        public float offsetFromWall = 0.3f;
        public float speedMultiplier = 0.2f;
        public float climbSpeed = 3;
        public float rotateSpeed = 5;
        public float distanceToWall = 1;
        public float distanceToMoveDirection = 1.0f;

        public IKSnapshot baseIKsnapshot;

        public FreeClimbAnimHook aHook;

        Transform helper;

        LayerMask ignoreLayers;

        ThirdPersonController tpc;

        // Use this for initialization
        void Start () {
            tpc = GetComponent<ThirdPersonController>();
            Init();
        }
        
        // Update is called once per frame
        public void Tick() {
            delta = Time.deltaTime;
            Tick(delta);
        }

        public void Init()
        {
            helper = new GameObject().transform;
            helper.name = "Climb Helper";
            aHook.Init(this, helper);
            ignoreLayers = ~(1 << 9);
        }

        public void Tick(float deltaTime)
        {
            this.delta = deltaTime;
            if(!inPosition)
            {
                GetInPosition();
                return;
            }

            if(!isLerping)
            {
                bool cancel = Input.GetButtonDown("Jump");
                if(cancel)
                {
                    CancelClimb();
                    return;
                }

                float hor = Input.GetAxis("Horizontal");
                float vert = Input.GetAxis("Vertical");
                float m = Mathf.Abs(hor) + Mathf.Abs(vert);

                Vector3 h = helper.right * hor;
                Vector3 v = helper.up * vert;
                Vector3 moveDir = (h + v).normalized;

                if(isMid)
                {
                    if(moveDir == Vector3.zero)
                    {
                        return;
                    }
                }
                else
                {
                    bool canMove = CanMove(moveDir);
                    if (!canMove || moveDir == Vector3.zero)
                    {
                        return;
                    }
                }

                isMid = !isMid;


                t = 0;
                isLerping = true;
                startPos = transform.position;
                Vector3 tp = helper.position - transform.position;
                float distance = Vector3.Distance(helper.position, startPos) / 2;
                tp *= positionOffset;
                tp += transform.position;
                targetPos = isMid ? tp : helper.position;

                aHook.CreatePositions(targetPos, moveDir, isMid);
            }
            else
            {
                t += delta * climbSpeed;
                if(t > 1)
                {
                    t = 1;
                    isLerping = false;
                }

                Vector3 cp = Vector3.Lerp(startPos, targetPos, t);
                transform.position = cp;
                transform.rotation = Quaternion.Slerp(transform.rotation, helper.rotation, delta * rotateSpeed);

                LookForGround();
            }
        }

        public bool CheckForClimb()
        {
            Vector3 origin = transform.position;
            origin.y += 0.02f;
            Vector3 dir = transform.forward;
            RaycastHit hit;
            if(Physics.Raycast(origin, dir, out hit, 0.5f, ignoreLayers))
            {
                helper.position = PosWithOffset(origin, hit.point);
                InitForClimb(hit);
                return true;
            }

            return false;
        }

        void InitForClimb(RaycastHit hit)
        {
            isClimbing = true;
            aHook.enabled = true;
            helper.transform.rotation = Quaternion.LookRotation(-hit.normal);
            startPos = transform.position;
            targetPos = hit.point + (hit.normal * offsetFromWall);
            t = 0;
            inPosition = false;
            anim.CrossFade("Hanging Idle", 2);
        }

        bool CanMove(Vector3 moveDir)
        {
            Vector3 origin = transform.position;
            float dis = distanceToMoveDirection;
            Vector3 dir = moveDir;
            DebugLine.singleton.SetLine(origin, origin + (dir * dis), 0);

            // Raycast desired direction
            RaycastHit hit;
            if(Physics.Raycast(origin, dir, out hit, dis))
            {
                // Check if corner
                return false;
            }

            origin += moveDir * dis;
            dir = helper.forward;

            float dis2 = distanceToWall;
            DebugLine.singleton.SetLine(origin, origin + (dir * dis2), 1);

            // Raycast towards wall
            if(Physics.Raycast(origin, dir, out hit, dis))
            {
                helper.position = PosWithOffset(origin, hit.point);
                helper.rotation = Quaternion.LookRotation(-hit.normal);
                return true;
            }

            origin = origin + (dir * dis2);
            dir = -moveDir;

            DebugLine.singleton.SetLine(origin, origin + dir, 1);
            // Raycast for inside corners
            if(Physics.Raycast(origin, dir, out hit, distanceToWall))
            {
                helper.position = PosWithOffset(origin, hit.point);
                helper.rotation = Quaternion.LookRotation(-hit.normal);
                return true;
            }

            origin += dir * dis2;
            dir = -Vector3.up;

            DebugLine.singleton.SetLine(origin, origin + dir, 2);

            if(Physics.Raycast(origin, dir, out hit, dis2))
            {
                float angle = Vector3.Angle(-helper.forward, hit.normal);
                if(angle < 40)
                {
                    helper.position = PosWithOffset(origin, hit.point);
                    helper.rotation = Quaternion.LookRotation(-hit.normal);
                    return true;
                }
            }

            return false;
        }
        void GetInPosition()
        {
            // transition time
            t += delta * 10;

            if(t > 1)
            {
                t = 1;
                inPosition = true;
                aHook.CreatePositions(targetPos, Vector3.zero, false);
            }

            Vector3 tp = Vector3.Lerp(startPos, targetPos, t);
            transform.position = tp;
            transform.rotation = Quaternion.Slerp(transform.rotation, helper.rotation, delta * rotateSpeed);
        }

        Vector3 PosWithOffset(Vector3 origin, Vector3 target)
        {
            Vector3 direction = origin - target;
            direction.Normalize();
            Vector3 offset = direction * offsetFromWall;
            return target + offset;
        }

        void LookForGround()
        {
            Vector3 origin = transform.position;
            Vector3 direction = -transform.up;
            RaycastHit hit;

            if(Physics.Raycast(origin, direction, out hit, distanceToMoveDirection + 0.05f, ignoreLayers))
            {
                CancelClimb();
            }
        }

        private void CancelClimb()
        {
            isClimbing = false;
            tpc.EnableController();
            aHook.enabled = false;
        }
    }

    [System.Serializable]
    public class IKSnapshot
    {
        public Vector3 rh, lh, rf, lf;
    }
}
