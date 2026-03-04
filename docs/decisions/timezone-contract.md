# Timezone Contract

Tripflow icin zaman kurali ikiye ayrilir:

- `DateOnly` + `TimeOnly` alanlari event-local wall time ifade eder.
- Loglar ve mutlak zaman damgalari UTC olarak saklanir ve tasinır.

Bu karar su anlama gelir:

- Event programindaki gun ve aktivite saatleri, event'in `TimeZoneId` degerine gore yorumlanir.
- Scheduled report, reminder ve due-job gibi hesaplar `Event.TimeZoneId` olmadan yapilmaz.
- Legacy event kayitlarinda `TimeZoneId` gecici olarak bos olabilir.
- `TimeZoneId` bos olan event'lerde zaman hesaplayan feature'lar `timezone_required` benzeri bir guard ile durdurulmalidir.

Bu sprintte:

- yeni event create/update akislarinda gecerli IANA timezone zorunludur
- mevcut event kayitlari nullable-first yaklasimi ile korunur
- sessiz server default timezone uygulanmaz
