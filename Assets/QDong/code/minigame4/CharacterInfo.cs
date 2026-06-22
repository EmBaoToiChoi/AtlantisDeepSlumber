using UnityEngine;
using Unity.Netcode;

public enum CharacterType
{
    Arthur,
    Leo,
    Maya,
    Elena
}

public class CharacterInfo : NetworkBehaviour
{
    // Biến này để bạn chọn loại nhân vật trong Inspector giống như cũ
    [SerializeField] private CharacterType typeInInspector;

    // Biến mạng dùng để đồng bộ trạng thái đi khắp các máy Client
    public NetworkVariable<CharacterType> characterType = new NetworkVariable<CharacterType>(
        CharacterType.Arthur, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // Khi nhân vật được sinh ra, nếu là Server thì áp đặt dữ liệu từ Inspector vào biến Mạng
        if (IsServer)
        {
            characterType.Value = typeInInspector;
        }
    }
}
