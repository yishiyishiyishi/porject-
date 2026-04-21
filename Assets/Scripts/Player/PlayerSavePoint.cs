using System;
using UnityEngine;
using Game.Core;
using Game.Framework.Save;

namespace Game.Player
{
    /// <summary>
    /// 玩家存档贡献者。订阅 SaveCaptureRequested/SaveRestoreRequested，
    /// 把玩家位置（与朝向）序列化进/出 SlotSaveData.blobs。
    ///
    /// 这是 ISaveable 模式的参考实现：SaveManager 完全不认识 Player，
    /// 业务组件通过事件自主贡献数据。后续的背包、任务、队伍状态都可以照抄本脚本。
    /// </summary>
    public class PlayerSavePoint : MonoBehaviour
    {
        private const string BlobId = "player";

        [Serializable]
        private class Blob
        {
            public float x, y;
            public int direction;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SaveCaptureRequested>(OnCapture);
            EventBus.Subscribe<SaveRestoreRequested>(OnRestore);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SaveCaptureRequested>(OnCapture);
            EventBus.Unsubscribe<SaveRestoreRequested>(OnRestore);
        }

        private void OnCapture(SaveCaptureRequested evt)
        {
            var actor = GetComponent<Game.Framework.Actor>();
            int dir = actor != null ? actor.State.Direction : 1;
            var blob = new Blob { x = transform.position.x, y = transform.position.y, direction = dir };
            SaveManager.WriteBlob(evt.Data, BlobId, blob);
        }

        private void OnRestore(SaveRestoreRequested evt)
        {
            var blob = SaveManager.ReadBlob<Blob>(evt.Data, BlobId);
            if (blob == null) return;
            transform.position = new Vector3(blob.x, blob.y, transform.position.z);
            var actor = GetComponent<Game.Framework.Actor>();
            if (actor != null) actor.SetDirection(blob.direction);
        }
    }
}
