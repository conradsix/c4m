-- Triggers for automatic path maintenance

-- Trigger function for INSERT and UPDATE operations
CREATE OR REPLACE FUNCTION cms.pages_path_trigger()
RETURNS TRIGGER AS $$
DECLARE
    current_id UUID := COALESCE(NEW.parent_id);
    current_slug TEXT;
    current_parent_id UUID;
    path_parts TEXT[] := '{}';
    full_path TEXT;
BEGIN
    -- For INSERT, calculate and set the path manually (can't use NEW.id since it's not set yet)
    IF TG_OP = 'INSERT' THEN
        -- Start with the current page's slug
        IF NEW.slug IS NULL OR NEW.slug = '' THEN
            NEW.path := '';
            RETURN NEW;
        END IF;
        
        path_parts := ARRAY[NEW.slug];
        
        -- Walk up the hierarchy collecting slugs from parents
        WHILE current_id IS NOT NULL LOOP
            SELECT pages.slug, pages.parent_id 
            INTO current_slug, current_parent_id
            FROM cms.pages 
            WHERE pages.id = current_id;
            
            -- If parent not found or slug is empty, break
            IF NOT FOUND OR current_slug IS NULL OR current_slug = '' THEN
                EXIT;
            END IF;
            
            -- Prepend parent slug to path parts
            path_parts := current_slug || path_parts;
            
            -- Move to next parent
            current_id := current_parent_id;
        END LOOP;
        
        -- Build final path
        full_path := '/' || array_to_string(path_parts, '/');
        NEW.path := full_path;
        RETURN NEW;
    END IF;
    
    -- For UPDATE, check if parent_id or slug changed
    IF TG_OP = 'UPDATE' THEN
        -- If parent_id or slug changed, recalculate path
        IF OLD.parent_id IS DISTINCT FROM NEW.parent_id OR OLD.slug IS DISTINCT FROM NEW.slug THEN
            -- Recalculate path using the same logic as INSERT
            current_id := NEW.parent_id;
            path_parts := '{}';
            
            -- Start with the current page's slug
            IF NEW.slug IS NULL OR NEW.slug = '' THEN
                NEW.path := '';
                RETURN NEW;
            END IF;
            
            path_parts := ARRAY[NEW.slug];
            
            -- Walk up the hierarchy collecting slugs from parents
            WHILE current_id IS NOT NULL LOOP
                SELECT pages.slug, pages.parent_id 
                INTO current_slug, current_parent_id
                FROM cms.pages 
                WHERE pages.id = current_id;
                
                -- If parent not found or slug is empty, break
                IF NOT FOUND OR current_slug IS NULL OR current_slug = '' THEN
                    EXIT;
                END IF;
                
                -- Prepend parent slug to path parts
                path_parts := current_slug || path_parts;
                
                -- Move to next parent
                current_id := current_parent_id;
            END LOOP;
            
            -- Build final path
            full_path := '/' || array_to_string(path_parts, '/');
            NEW.path := full_path;
        END IF;
        RETURN NEW;
    END IF;
    
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Trigger function for AFTER UPDATE to handle children
CREATE OR REPLACE FUNCTION cms.pages_path_after_trigger()
RETURNS TRIGGER AS $$
BEGIN
    -- If parent_id or slug changed, update all children
    IF OLD.parent_id IS DISTINCT FROM NEW.parent_id OR OLD.slug IS DISTINCT FROM NEW.slug THEN
        PERFORM cms.update_children_paths(NEW.id);
    END IF;
    
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Create the triggers
CREATE TRIGGER pages_path_before_trigger
    BEFORE INSERT OR UPDATE ON cms.pages
    FOR EACH ROW
    EXECUTE FUNCTION cms.pages_path_trigger();

CREATE TRIGGER pages_path_after_trigger
    AFTER UPDATE ON cms.pages
    FOR EACH ROW
    EXECUTE FUNCTION cms.pages_path_after_trigger();
