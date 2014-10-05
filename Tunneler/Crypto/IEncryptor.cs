namespace Tunneler.Crypto
{
    public interface IEncryptor
    {
		void SetKeys (byte[] publicKey, byte[] secretKey);
		byte[] Encrypt (byte[] data);
		byte[] Decrypt (byte[] data);
    }
}