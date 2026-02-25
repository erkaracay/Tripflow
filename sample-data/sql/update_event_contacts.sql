-- Tripflow event contacts manual update script (PostgreSQL)
-- 1) Fill event_id and organization_id
-- 2) Replace contact values
-- 3) Run whole script

WITH input AS (
  SELECT
    '00000000-0000-0000-0000-000000000000'::uuid AS event_id,
    '00000000-0000-0000-0000-000000000000'::uuid AS organization_id,
    'Etkinlik Rehberi'::varchar(200) AS guide_name,
    '+905551112233'::varchar(50) AS guide_phone,
    'Grup Lideri'::varchar(200) AS leader_name,
    '+905551112244'::varchar(50) AS leader_phone,
    '+905551112255'::varchar(50) AS emergency_phone,
    'https://chat.whatsapp.com/example'::varchar(500) AS whatsapp_group_url
)
UPDATE events e
SET
  "GuideName" = i.guide_name,
  "GuidePhone" = i.guide_phone,
  "LeaderName" = i.leader_name,
  "LeaderPhone" = i.leader_phone,
  "EmergencyPhone" = i.emergency_phone,
  "WhatsappGroupUrl" = i.whatsapp_group_url
FROM input i
WHERE e."Id" = i.event_id
  AND e."OrganizationId" = i.organization_id;

SELECT
  e."Id",
  e."OrganizationId",
  e."Name",
  e."GuideName",
  e."GuidePhone",
  e."LeaderName",
  e."LeaderPhone",
  e."EmergencyPhone",
  e."WhatsappGroupUrl"
FROM events e
WHERE e."Id" = '00000000-0000-0000-0000-000000000000'::uuid;
