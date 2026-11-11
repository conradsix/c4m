CREATE OR REPLACE FUNCTION cms.get_menu_items(p_menu_id UUID)
RETURNS TABLE (
    id UUID,
    menu_id UUID,
    parent_id UUID,
    action_type_id UUID,
    title VARCHAR(200),
    sort_order INT,
    action_data JSONB,
    created_at TIMESTAMPTZ
) AS $$
WITH RECURSIVE menu_tree AS (
    -- Start with all root items of the specified menu
    SELECT
        mi.id,
        mi.menu_id,
        mi.parent_id,
        mi.action_type_id,
        mi.title,
        mi.sort_order,
        mi.action_data,
        mi.created_at,
        0 AS depth
    FROM cms.menu_items mi
    WHERE mi.menu_id = p_menu_id

    UNION ALL

    -- Recursively get all children
    SELECT
        mi.id,
        mi.menu_id,
        mi.parent_id,
        mi.action_type_id,
        mi.title,
        mi.sort_order,
        mi.action_data,
        mi.created_at,
        mt.depth + 1
    FROM cms.menu_items mi
    INNER JOIN menu_tree mt ON mi.parent_id = mt.id
)
SELECT
    id,
    menu_id,
    parent_id,
    action_type_id,
    title,
    sort_order,
    action_data,
    created_at
FROM menu_tree
ORDER BY depth, parent_id, sort_order;
$$ LANGUAGE SQL STABLE;
