using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ItemStandAllItems;
public class Attacher
{
  public static bool Enabled(ItemStand? obj) => obj != null && obj.m_nview && Configuration.ItemStandIds.Contains(Utils.GetPrefabName(obj.transform.root.gameObject).ToLower());
  ///<summary>Legacy only finds the object with a collider. May not contain all models of the item resulting only in a partial item (like Graydward eye will miss the eye).</summary>
  private static GameObject? GetAttachObjectLegacy(GameObject item)
  {
    var collider = item.transform.GetComponentInChildren<Collider>();
    return collider ? collider.transform.gameObject : null;
  }

  ///<summary>Returns the only child (if possible).</summary>
  private static GameObject? GetChildModel(GameObject item)
  {
    GameObject? onlyChild = null;
    // Prioritize the item layer (some have secondary transformations for equipping).
    foreach (Transform child in item.transform)
    {
      if (child.gameObject.layer != item.layer) continue;
      if (onlyChild) return null;
      onlyChild = child.gameObject;
    }
    if (onlyChild) return onlyChild;
    // Accept other layers as well (Acorn fix).
    foreach (Transform child in item.transform)
    {
      if (onlyChild) return null;
      onlyChild = child.gameObject;
    }
    return onlyChild;
  }
  ///<summary>Finds a given transform. Copypaste from base game code.</summary>
  private static GameObject? GetTransform(GameObject item, string name)
  {
    var transform = item.transform.Find(name);
    return transform ? transform.gameObject : null;
  }
  public static GameObject? GetAttach(GameObject item)
  {
    // Base game also uses "attach" transform but explicitly disabled for some items.
    // Check it first as it's the safest pick.
    var obj = GetTransform(item, "attach");
    if (obj) return obj;
    if (Configuration.Mode != "All") return null;
    if (Configuration.UseLegacyAttaching) return GetAttachObjectLegacy(item);
    // Child object is preferred as it won't contain ItemDrop script or weird transformation.
    var childModel = GetChildModel(item);
    if (childModel)
      return childModel;
    return item;
  }

  ///<summary>Hides the item stand if it has an item.</summary>
  public static void HideIfItem(ItemStand obj)
  {
    var zdo = obj.m_nview.GetZDO();
    var hideValue = zdo.GetInt(ItemStandCommand.HashHide, 0);
    if (hideValue == 0) hideValue = Configuration.HideAutomatically ? 1 : -1;
    var item = obj.m_visualItem;
    var show = !obj.HaveAttachment() || hideValue < 1;
    // Layer check to filter the attached item.
    var renderers = obj.GetComponentsInChildren<MeshRenderer>().Where(renderer => item == null || renderer.gameObject.layer == obj.gameObject.layer);
    foreach (var renderer in renderers)
    {
      if (renderer.enabled != show)
        renderer.enabled = show;
    }
  }
  ///<summary>Enables disabled attach transformations.</summary>
  public static void Enable(ItemStand obj)
  {
    if (!obj.m_visualItem) return;
    if (obj.m_visualItem.activeSelf) return;
    // Probably safest to hardcode this for now. Maybe something more generic if more cases appear.
    var equipped = obj.m_visualItem.transform.Find("equiped");
    if (equipped)
    {
      // This will get saved to the OriginalPositions so must be done regardless of settings.
      obj.m_visualItem.transform.localPosition = Vector3.zero;
      equipped.gameObject.SetActive(true);
      if (equipped.gameObject.TryGetComponent<Rigidbody>(out var body))
        body.isKinematic = true;
    }
    obj.m_visualItem.SetActive(Configuration.Mode == "All");
  }
  ///<summary>Updates local transformation according to settings.</summary>
  public static void UpdateItemTransform(ItemStand obj)
  {
    if (obj.m_visualItem == null) return;
    var transformations = Configuration.CustomTransformations();
    Configuration.Offset(transformations, obj);
    Configuration.Rotate(transformations, obj);
    Configuration.Scale(transformations, obj);
  }
  ///<summary>Replaces ItemDrop script with an empty dummy object.</summary>
  public static void ReplaceItemDrop(ItemStand obj)
  {
    var item = obj.m_visualItem;
    if (item == null || item.GetComponent<ItemDrop>() == null) return;
    var attach = item.transform.parent;
    var dummy = Object.Instantiate<GameObject>(new(), attach.position, attach.rotation, attach);
    dummy.layer = item.layer;
    List<GameObject> children = [];
    foreach (Transform child in item.transform)
    {
      if (child.gameObject.layer != dummy.layer) continue;
      children.Add(child.gameObject);
    }
    foreach (GameObject child in children)
      child.transform.SetParent(dummy.transform, false);
    // Using ZNetScene shouldn't be necessary but is safer.
    ZNetScene.instance.Destroy(item);
    obj.m_visualItem = dummy;
  }

  public static void Refresh(ItemStand obj)
  {
    if (!Enabled(obj)) return;
    ReplaceItemDrop(obj);
    Enable(obj);
    UpdateItemTransform(obj);
    HideIfItem(obj);
  }
  public static void ReloadAll()
  {
    // This gets called on close (server sync changes stuff) so make sure the game is actually running.
    if (!ZNet.instance || ZNet.instance.HaveStopped) return;
    foreach (var obj in Object.FindObjectsOfType<ItemStand>())
      Reload(obj);
  }

  public static void Reload(ItemStand obj)
  {
    // Ensures a full refresh.
    Object.Destroy(obj.m_visualItem);
    obj.m_visualName = "";
    obj.UpdateVisual();
  }
}
