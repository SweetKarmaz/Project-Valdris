using UnityEditor;
using UnityEngine;

// Tools > Valdris > Setup Projectile Prefabs
//
// Scans every prefab in Assets/_Project/Prefabs/WeaponProjectiles/ and ensures
// each one has the three components required for runtime flight and damage:
//
//   Rigidbody          — useGravity off, continuous collision detection,
//                        low mass (0.05 kg), no drag (we control physics manually)
//   CapsuleCollider    — IsTrigger on, sized for a typical arrow shaft
//                        (radius 0.02, height 0.55, Z-axis)
//   ProjectileBehaviour — the Valdris runtime flight/damage script
//
// Existing components are left unchanged; only missing ones are added.
// Run again at any time — it is safe to re-run on already-setup prefabs.
public static class ProjectilePrefabSetup
{
    const string ProjectileFolder = "Assets/_Project/Prefabs/WeaponProjectiles";

    [MenuItem("Tools/Valdris/Weapons/Setup Projectile Prefabs")]
    public static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ProjectileFolder });
        int modified = 0;

        foreach (string guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // Open the prefab for editing via PrefabUtility so changes are
            // saved back to the asset rather than a scene instance.
            var contents = PrefabUtility.LoadPrefabContents(path);
            bool changed  = false;

            // ── Rigidbody ─────────────────────────────────────────────────────
            var rb = contents.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = contents.AddComponent<Rigidbody>();
                changed = true;
            }
            // Always enforce the required settings even if Rigidbody existed.
            if (rb.useGravity != false)           { rb.useGravity            = false;                          changed = true; }
            if (rb.isKinematic)                   { rb.isKinematic           = false;                          changed = true; }
            if (rb.mass != 0.05f)                 { rb.mass                  = 0.05f;                          changed = true; }
            if (rb.linearDamping != 0f)           { rb.linearDamping         = 0f;                             changed = true; }
            if (rb.angularDamping != 0f)          { rb.angularDamping        = 0f;                             changed = true; }
            if (rb.interpolation != RigidbodyInterpolation.Interpolate)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                changed = true;
            }
            if (rb.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                changed = true;
            }
            // Freeze rotation — we orient the arrow via transform.rotation in code.
            RigidbodyConstraints wantConstraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;
            if (rb.constraints != wantConstraints) { rb.constraints = wantConstraints; changed = true; }

            // ── CapsuleCollider (trigger) ──────────────────────────────────────
            // Only add one if NO collider of any kind is present.
            var anyCollider = contents.GetComponent<Collider>();
            if (anyCollider == null)
            {
                var cap       = contents.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.direction = 2;      // Z-axis — arrow points along local Z
                cap.radius    = 0.02f;  // thin shaft
                cap.height    = 0.55f;  // ~half a metre for a standard arrow
                cap.center    = Vector3.zero;
                changed       = true;
            }
            else if (!anyCollider.isTrigger)
            {
                anyCollider.isTrigger = true;
                changed = true;
            }

            // ── ProjectileBehaviour ────────────────────────────────────────────
            if (contents.GetComponent<ProjectileBehaviour>() == null)
            {
                contents.AddComponent<ProjectileBehaviour>();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                modified++;
                Debug.Log($"[ProjectilePrefabSetup] Updated: {path}");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Projectile Prefab Setup",
            $"Done. {modified} prefab(s) updated out of {guids.Length} scanned.",
            "OK");
    }
}
