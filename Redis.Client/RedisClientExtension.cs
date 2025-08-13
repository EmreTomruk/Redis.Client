namespace Redis.Client;

public static class RedisClientExtension
{
    public static int GetDeterministicHashCode(this string value)
    {
        unchecked
        {
            var hash1 = (5381 << 16) + 5381;
            var hash2 = hash1;

            var input = value.ToLower();

            for (var i = 0; i < input.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ input[i];
                if (i == input.Length - 1)
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ input[i + 1];
            }

            var result = hash1 + (hash2 * 1566083941);

            if (result < 0)
                result *= -1;

            return result;
        }
    }
}