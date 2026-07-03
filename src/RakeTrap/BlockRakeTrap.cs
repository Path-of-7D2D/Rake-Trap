using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace RakeTrap
{
    [Preserve]
    public class BlockRakeTrap : Block
    {
        private const string PropTriggerSound = "TriggerSound";
        private const string PropDamageType = "DamageType";
        private const string PropKnockdownChance = "KnockdownChance";
        private const string PropBrushOffChance = "BrushOffChance";
        private const string PropRearmSeconds = "RearmSeconds";
        private const string PropAnimationTrigger = "AnimationTrigger";
        private const string PropTrapDamage = "Damage";
        private const string PropArmorPierceFraction = "ArmorPierceFraction";

        private static readonly Vector3 ModelBoundsCenter = new Vector3(0f, 0.105f, -0.9f);
        private static readonly Vector3 ModelBoundsSize = new Vector3(0.6f, 0.25f, 1.85f);
        private static readonly Dictionary<Vector3i, float> RearmTimes = new Dictionary<Vector3i, float>();

        private int damage = 4;
        private EnumDamageTypes damageType = EnumDamageTypes.Bashing;
        private float knockdownChance = 0.9f;
        private float brushOffChance = 0.12f;
        private float rearmSeconds = 4f;
        private string triggerSound = "trap3x3WoodTrigger";
        private string animationTrigger = "Spring";

        public BlockRakeTrap()
        {
            IsCheckCollideWithEntity = true;
        }

        public override void Init()
        {
            base.Init();
            ParseInt(PropTrapDamage, ref damage);
            ParseEnum(PropDamageType, ref damageType);
            ParseChance(PropKnockdownChance, ref knockdownChance);
            ParseChance(PropBrushOffChance, ref brushOffChance);
            ParseFloat(PropRearmSeconds, ref rearmSeconds);
            ParseString(PropTriggerSound, ref triggerSound);
            ParseString(PropAnimationTrigger, ref animationTrigger);
        }

        public override void GetCollisionAABB(BlockValue _blockValue, int _x, int _y, int _z, float _distortedY, List<Bounds> _result)
        {
            Quaternion rotation = shape.GetRotation(_blockValue);
            Vector3 center = new Vector3(_x + 0.5f, _y, _z + 0.5f) + rotation * ModelBoundsCenter;
            Vector3 halfSize = ModelBoundsSize * 0.5f;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 extents = new Vector3(
                Mathf.Abs(right.x) * halfSize.x + Mathf.Abs(up.x) * halfSize.y + Mathf.Abs(forward.x) * halfSize.z,
                Mathf.Abs(right.y) * halfSize.x + Mathf.Abs(up.y) * halfSize.y + Mathf.Abs(forward.y) * halfSize.z,
                Mathf.Abs(right.z) * halfSize.x + Mathf.Abs(up.z) * halfSize.y + Mathf.Abs(forward.z) * halfSize.z);

            center.y += _distortedY * 0.5f;
            extents.y += _distortedY * 0.5f;
            _result.Add(new Bounds(center, extents * 2f));
        }

        public override bool IsMovementBlocked(IBlockAccess _world, Vector3i _blockPos, BlockValue _blockValue, BlockFace crossingFace)
        {
            return false;
        }

        public override float GetStepHeight(IBlockAccess world, Vector3i blockPos, BlockValue _blockValue, BlockFace crossingFace)
        {
            return 0.12f;
        }

        public override bool CanPlaceBlockAt(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, bool _bOmitCollideCheck = false)
        {
            if (!base.CanPlaceBlockAt(_world, _blockPos, _blockValue, _bOmitCollideCheck))
            {
                return false;
            }

            BlockValue support = _world.GetBlock(_blockPos - Vector3i.up);
            Block supportBlock = support.Block;
            if (support.isair || supportBlock == null || supportBlock.blockMaterial == null || supportBlock.blockMaterial.IsLiquid)
            {
                return false;
            }

            return supportBlock.StabilitySupport;
        }

        public override void OnBlockStartsToFall(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
        {
            // Surface traps are props, not structural blocks. Falling physics deletes them.
        }

        public override bool ShowModelOnFall()
        {
            return false;
        }

        public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
        {
            base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
            if (_ebcd == null || _ebcd.transform == null)
            {
                return;
            }

            BoxCollider collider = _ebcd.transform.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = _ebcd.transform.gameObject.AddComponent<BoxCollider>();
                collider.center = ModelBoundsCenter;
                collider.size = ModelBoundsSize;
                if (ChunkCluster.LayerMappingTable.TryGetValue("nocollision", out int noCollisionLayer))
                {
                    collider.gameObject.layer = noCollisionLayer;
                }
            }

            // Voxel.Raycast only resolves entity-model blocks when the hit collider is
            // tagged T_Block; tag at runtime because bundles serialize tags by index.
            Collider[] colliders = _ebcd.transform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].gameObject.tag = "T_Block";
            }
        }

        public override void OnEntityWalking(WorldBase _world, int _x, int _y, int _z, BlockValue _blockValue, Entity entity)
        {
            TryTrigger(_world, new Vector3i(_x, _y, _z), _blockValue, entity);
        }

        public override bool OnEntityCollidedWithBlock(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, Entity _targetEntity)
        {
            TryTrigger(_world, _blockPos, _blockValue, _targetEntity);
            return false;
        }

        private bool TryTrigger(WorldBase world, Vector3i blockPos, BlockValue blockValue, Entity entity)
        {
            if (world == null || world.IsRemote())
            {
                return false;
            }

            EntityAlive alive = entity as EntityAlive;
            if (alive == null || alive.IsDead())
            {
                return false;
            }

            EntityPlayer player = alive as EntityPlayer;
            if (player != null && player.IsSpectator)
            {
                return false;
            }

            float now = Time.time;
            if (RearmTimes.TryGetValue(blockPos, out float armedAt) && now < armedAt)
            {
                return false;
            }

            RearmTimes[blockPos] = now + rearmSeconds;

            PlayTriggerSound(world, blockPos, alive);
            AnimateTrap(world, blockPos);
            ApplyTrapEffect(world, blockPos, blockValue, alive);
            return true;
        }

        private void ApplyTrapEffect(WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityAlive target)
        {
            int trapDamage = GetTrapDamage(blockValue);
            if (trapDamage > 0)
            {
                Vector3 direction = target.position - new Vector3(blockPos.x + 0.5f, blockPos.y + 0.15f, blockPos.z + 0.5f);
                if (direction.sqrMagnitude < 0.001f)
                {
                    direction = Vector3.up;
                }

                int armorPierceDamage = Mathf.Clamp(Mathf.RoundToInt(trapDamage * GetArmorPierceFraction(blockValue)), 0, trapDamage);
                int normalDamage = trapDamage - armorPierceDamage;
                if (normalDamage > 0)
                {
                    DamageSourceEntity source = CreateDamageSource(blockValue, blockPos, direction, EnumDamageSource.External, damageType);
                    target.DamageEntity(source, normalDamage, _criticalHit: false, 0.2f);
                }

                if (armorPierceDamage > 0)
                {
                    DamageSourceEntity pierceSource = CreateDamageSource(blockValue, blockPos, direction, EnumDamageSource.Internal, EnumDamageTypes.Piercing);
                    target.DamageEntity(pierceSource, armorPierceDamage, _criticalHit: false, 0f);
                }
            }

            if (target.Buffs == null)
            {
                return;
            }

            bool brushedOff = RandomFloat(world) < brushOffChance;
            if (!brushedOff && RandomFloat(world) < knockdownChance)
            {
                target.Buffs.AddBuff("buffInjuryKnockdown01", blockPos);
            }
        }

        private int GetTrapDamage(BlockValue blockValue)
        {
            int trapDamage = damage;
            Block block = blockValue.Block;
            if (block != null &&
                block.Properties != null &&
                block.Properties.Values.TryGetValue(PropTrapDamage, out string raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                trapDamage = parsed;
            }

            return trapDamage;
        }

        private float GetArmorPierceFraction(BlockValue blockValue)
        {
            float fraction = 0f;
            Block block = blockValue.Block;
            if (block != null &&
                block.Properties != null &&
                block.Properties.Values.TryGetValue(PropArmorPierceFraction, out string raw) &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                fraction = parsed;
            }

            return Mathf.Clamp01(fraction);
        }

        private DamageSourceEntity CreateDamageSource(
            BlockValue blockValue,
            Vector3i blockPos,
            Vector3 direction,
            EnumDamageSource source,
            EnumDamageTypes type)
        {
            return new DamageSourceEntity(source, type, -1, direction.normalized)
            {
                AttackingItem = blockValue.ToItemValue(),
                BlockPosition = blockPos
            };
        }

        private void PlayTriggerSound(WorldBase world, Vector3i blockPos, EntityAlive target)
        {
            if (string.IsNullOrEmpty(triggerSound) || GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.PlaySoundAtPositionServer(
                new Vector3(blockPos.x + 0.5f, blockPos.y + 0.15f, blockPos.z + 0.5f),
                triggerSound,
                AudioRolloffMode.Linear,
                12,
                target.entityId);
        }

        private void AnimateTrap(WorldBase world, Vector3i blockPos)
        {
            if (string.IsNullOrEmpty(animationTrigger))
            {
                return;
            }

            if (!GameManager.IsDedicatedServer && world is World liveWorld && liveWorld.ChunkCache != null)
            {
                BlockEntityData blockEntity = liveWorld.ChunkCache.GetBlockEntity(blockPos);
                if (blockEntity != null && blockEntity.transform != null)
                {
                    Animator[] animators = blockEntity.transform.GetComponentsInChildren<Animator>();
                    for (int i = 0; i < animators.Length; i++)
                    {
                        animators[i].enabled = true;
                        animators[i].SetTrigger(animationTrigger);
                    }
                }
            }

            ConnectionManager connectionManager = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (connectionManager != null && connectionManager.IsServer)
            {
                connectionManager.SendPackage(NetPackageManager.GetPackage<NetPackageAnimateBlock>().Setup(blockPos, animationTrigger));
            }
        }

        private float RandomFloat(WorldBase world)
        {
            GameRandom random = world.GetGameRandom();
            return random != null ? random.RandomFloat : UnityEngine.Random.value;
        }

        private void ParseString(string name, ref string value)
        {
            if (Properties.Values.TryGetValue(name, out string raw) && !string.IsNullOrEmpty(raw))
            {
                value = raw;
            }
        }

        private void ParseInt(string name, ref int value)
        {
            if (Properties.Values.TryGetValue(name, out string raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                value = parsed;
            }
        }

        private void ParseFloat(string name, ref float value)
        {
            if (Properties.Values.TryGetValue(name, out string raw) &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                value = parsed;
            }
        }

        private void ParseChance(string name, ref float value)
        {
            ParseFloat(name, ref value);
            if (value > 1f)
            {
                value /= 100f;
            }

            value = Mathf.Clamp01(value);
        }

        private void ParseEnum<T>(string name, ref T value) where T : struct
        {
            if (Properties.Values.TryGetValue(name, out string raw) &&
                Enum.TryParse(raw, ignoreCase: true, result: out T parsed))
            {
                value = parsed;
            }
        }
    }
}
