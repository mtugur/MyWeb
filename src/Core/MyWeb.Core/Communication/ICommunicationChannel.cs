using System;
using System.Collections.Generic;

namespace MyWeb.Core.Communication
{
    /// <summary>
    /// PLC iletişimi için temel sözleşme.
    /// </summary>
    public interface ICommunicationChannel : IDisposable
    {
        /// <summary>
        /// PLC bağlantılarını açar.
        /// </summary>
        /// <returns>Tüm bağlantılar başarılıysa true, değilse false.</returns>
        bool Connect();

        /// <summary>
        /// PLC bağlantılarını kapatır.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Tüm bağlantıların açık olup olmadığını gösterir.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Tek bir tag’in değerini okur.
        /// </summary>
        /// <typeparam name="T">Dönüş tipi (bool, int, float, struct vb.).</typeparam>
        /// <param name="tagName">Önceden tanımlanmış tag adı.</param>
        /// <returns>Tag’in read edilen değeri.</returns>
        T ReadTag<T>(string tagName);

        /// <summary>
        /// Birden fazla tag’in değerini okur.
        /// </summary>
        /// <param name="tagNames">Okunacak tag adları.</param>
        /// <returns>tagName → değeri sözlüğü.</returns>
        Dictionary<string, object> ReadTags(IEnumerable<string> tagNames);

        /// <summary>
        /// Tek bir tag’e değer yazar.
        /// </summary>
        /// <param name="tagName">Yazılacak tag adı.</param>
        /// <param name="value">Gönderilecek değer.</param>
        /// <returns>Başarılıysa true, değilse false.</returns>
        bool WriteTag(string tagName, object value);

        /// <summary>
        /// Yeni bir tag tanımı ekler.
        /// </summary>
        /// <param name="tagDefinition">Tag’in adı, adresi vb. bilgileri.</param>
        void AddTag(TagDefinition tagDefinition);

        /// <summary>
        /// Varolan bir tag tanımını kaldırır.
        /// </summary>
        /// <param name="tagName">Silinecek tag adı.</param>
        /// <returns>Bulup sildiyse true, yoksa false.</returns>
        bool RemoveTag(string tagName);
    }
}
