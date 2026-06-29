#if UNITY_EDITOR
using Player.Stasis;
using UIScripts.FeedBack_UI.Crosshair;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class StasisProxyCreator
    {
        private enum ColliderShape { Sphere, Box }

        // ================= MENU ITEMS =================

        [MenuItem("Tools/Stasis/Create Proxy (Sphere) on Selected")]
        private static void CreateSphereProxyOnSelected()
        {
            CreateProxyOnSelected(ColliderShape.Sphere);
        }

        [MenuItem("Tools/Stasis/Create Proxy (Box) on Selected")]
        private static void CreateBoxProxyOnSelected()
        {
            CreateProxyOnSelected(ColliderShape.Box);
        }

        // ================ CORE LOGIC ==================

        private static void CreateProxyOnSelected(ColliderShape shape)
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[StasisProxyCreator] No hay ningún GameObject seleccionado.");
                return;
            }

            int layerStasisProxy = LayerMask.NameToLayer("StasisProxy");
            if (layerStasisProxy == -1)
            {
                Debug.LogWarning("[StasisProxyCreator] No existe la layer 'StasisProxy'. Creala primero en Project Settings > Tags and Layers.");
            }

            foreach (var root in selection)
            {
                if (root == null) continue;

                // 1) Buscar IStasis en este GO o en sus padres
                IStasis stasis = null;
                MonoBehaviour stasisOwner = null;

                var monos = root.GetComponentsInParent<MonoBehaviour>(true);
                for (int i = 0; i < monos.Length; i++)
                {
                    if (monos[i] is IStasis s)
                    {
                        stasis = s;
                        stasisOwner = monos[i];
                        break;
                    }
                }

                if (stasis == null || stasisOwner == null)
                {
                    Debug.LogWarning($"[StasisProxyCreator] '{root.name}' no tiene ningún componente que implemente IStasis en él o sus padres. Lo salto.", root);
                    continue;
                }

                // 2) Obtener bounds de los Renderer hijos para ubicar el proxy
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = new Bounds(root.transform.position, Vector3.one * 0.5f);
                bool hasBounds = false;

                if (renderers != null && renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    hasBounds = true;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);
                }
                else
                {
                    Debug.LogWarning($"[StasisProxyCreator] '{root.name}' no tiene Renderer en hijos. Uso posición del transform.", root);
                }

                // 3) Crear GO hijo "StasisProxy"
                var proxyGo = new GameObject("StasisProxy");
                Undo.RegisterCreatedObjectUndo(proxyGo, "Create Stasis Proxy");

                proxyGo.transform.SetParent(root.transform, worldPositionStays: true);
                proxyGo.transform.position = hasBounds ? bounds.center : root.transform.position;
                proxyGo.transform.rotation = root.transform.rotation;
                proxyGo.transform.localScale = Vector3.one;

                if (layerStasisProxy != -1)
                    proxyGo.layer = layerStasisProxy;

                // 4) Agregar componente StasisProxy
                var proxy = Undo.AddComponent<StasisProxy>(proxyGo);
                proxy.owner = stasisOwner;
                proxy.autoCreateSphere = false; // NO auto-crear, porque vamos a poner nuestro collider manualmente

                // 5) Crear collider según la forma elegida
                switch (shape)
                {
                    case ColliderShape.Sphere:
                        CreateSphereCollider(proxyGo, proxy, bounds, hasBounds);
                        Debug.Log($"[StasisProxyCreator] Proxy (Sphere) creado para '{root.name}' en '{proxyGo.name}'.", root);
                        break;

                    case ColliderShape.Box:
                        CreateBoxCollider(proxyGo, proxy, bounds, hasBounds);
                        Debug.Log($"[StasisProxyCreator] Proxy (Box) creado para '{root.name}' en '{proxyGo.name}'.", root);
                        break;
                }
            }
        }

        private static void CreateSphereCollider(GameObject proxyGo, StasisProxy proxy, Bounds bounds, bool hasBounds)
        {
            var sc = Undo.AddComponent<SphereCollider>(proxyGo);
            sc.isTrigger = true;

            float radius = 0.5f;
            if (hasBounds)
            {
                // diagonal del bounds, moderada
                float diag = bounds.extents.magnitude;
                radius = Mathf.Max(0.1f, diag * 0.25f);
            }
            sc.radius = radius;

            proxy.proxyCollider = sc;
            proxy.defaultRadius = radius;
        }

        private static void CreateBoxCollider(GameObject proxyGo, StasisProxy proxy, Bounds bounds, bool hasBounds)
        {
            var bc = Undo.AddComponent<BoxCollider>(proxyGo);
            bc.isTrigger = true;

            if (hasBounds)
            {
                // Transformar center/size a espacio local del proxy
                Vector3 centerLocal = proxyGo.transform.InverseTransformPoint(bounds.center);
                Vector3 sizeLocal = bounds.size; // ya está en mundo, pero como el proxy es hijo sin escalar, sirve bien

                bc.center = centerLocal;
                bc.size  = sizeLocal;
            }
            else
            {
                bc.center = Vector3.zero;
                bc.size   = Vector3.one * 0.5f;
            }

            proxy.proxyCollider = bc;
            proxy.defaultRadius = 0.5f; // solo para que no quede 0; no se usa para Box realmente
        }
    }
}
#endif
