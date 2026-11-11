-- Parent validation functions for pages table

-- Function to detect cycles in page hierarchy
CREATE OR REPLACE FUNCTION cms.validate_no_cycles(
    page_id UUID,
    new_parent_id UUID
)
RETURNS BOOLEAN AS $$
DECLARE
    current_ancestor_id UUID;
BEGIN
    -- If new_parent_id is NULL, no cycle possible
    IF new_parent_id IS NULL THEN
        RETURN TRUE;
    END IF;
    
    -- If trying to set parent to self, that's a cycle
    IF page_id = new_parent_id THEN
        RETURN FALSE;
    END IF;
    
    -- Walk up the hierarchy from the new parent to check if we encounter the page_id
    current_ancestor_id := new_parent_id;
    
    WHILE current_ancestor_id IS NOT NULL LOOP
        -- If we find the page_id in the ancestry chain, it would create a cycle
        IF current_ancestor_id = page_id THEN
            RETURN FALSE;
        END IF;
        
        -- Move to the next parent
        SELECT parent_id INTO current_ancestor_id
        FROM cms.pages
        WHERE id = current_ancestor_id;
        
        -- If parent not found, break the loop
        IF NOT FOUND THEN
            EXIT;
        END IF;
    END LOOP;
    
    -- No cycle detected
    RETURN TRUE;
END;
$$ LANGUAGE plpgsql;
